"""PPO and behavioural-cloning updates. Runs on the training device (MPS on an M3)."""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Dict, List, Optional

import numpy as np
import torch
import torch.nn.functional as F

from encoder import collate
from model import to_tensors


@dataclass
class PPOConfig:
    gamma: float = 0.995
    gae_lambda: float = 0.95
    clip: float = 0.2
    epochs: int = 3
    minibatch: int = 1024
    lr: float = 2e-4
    vf_coef: float = 0.5
    ent_coef: float = 0.01
    max_grad_norm: float = 0.5
    target_kl: float = 0.03
    # "last": stop the epochs when the last minibatch's approx KL exceeds target_kl (V1).
    # "mean": when the epoch's mean approx KL does (a single minibatch is noisy).
    kl_stop: str = "last"
    # Two-hot cross-entropy divided by log(value_bins): ~1 at init, like the MSE of a
    # scalar head, so vf_coef means the same for both heads. False = the raw CE.
    value_ce_norm: bool = True


@dataclass
class Budget:
    """A run's budget: whichever of iterations, hours of training time and
    training states runs out first (None = no limit on that one). Its progress
    in [0, 1] also drives the LR and entropy schedules, so two runs with the
    same `states` budget anneal at the same states whatever their speed."""
    iterations: Optional[int] = None
    hours: Optional[float] = None
    states: Optional[int] = None

    def progress(self, iteration: int, seconds: float, states: int) -> float:
        p = 0.0
        if self.iterations:
            p = max(p, iteration / self.iterations)
        if self.hours:
            p = max(p, seconds / (3600 * self.hours))
        if self.states:
            p = max(p, states / self.states)
        return p


@dataclass
class Schedule:
    """LR: linear warmup over `warmup_iters`, then cosine from `lr` down to
    `lr * lr_min_ratio` over the run's progress (lr_min_ratio 1.0 = constant).
    Entropy coefficient: linear from `ent_start` to `ent_end`."""
    lr: float = 2e-4
    warmup_iters: int = 0
    lr_min_ratio: float = 1.0
    ent_start: float = 0.01
    ent_end: float = 0.01

    def lr_at(self, iteration: int, progress: float) -> float:
        if self.warmup_iters and iteration <= self.warmup_iters:
            return self.lr * iteration / self.warmup_iters
        cosine = 0.5 * (1 + math.cos(math.pi * min(max(progress, 0.0), 1.0)))
        return self.lr * (self.lr_min_ratio + (1 - self.lr_min_ratio) * cosine)

    def ent_at(self, progress: float) -> float:
        p = min(max(progress, 0.0), 1.0)
        return self.ent_start + (self.ent_end - self.ent_start) * p


def two_hot(targets: torch.Tensor, support: torch.Tensor) -> torch.Tensor:
    """[N] scalars -> [N, bins] distributions whose expectation over `support` is
    the target (clamped into the support's range): the mass is split between
    the two nearest bins, proportionally to the distance."""
    lo, hi, bins = support[0], support[-1], support.numel()
    pos = (targets.clamp(lo, hi) - lo) / (hi - lo) * (bins - 1)
    below = pos.floor().clamp(max=bins - 2)
    frac = pos - below
    out = torch.zeros(targets.shape[0], bins, device=targets.device, dtype=support.dtype)
    out.scatter_(1, below.long().unsqueeze(1), (1 - frac).unsqueeze(1))
    out.scatter_add_(1, below.long().unsqueeze(1) + 1, frac.unsqueeze(1))
    return out


def value_loss(model, values: torch.Tensor, value_logits: Optional[torch.Tensor],
               targets: torch.Tensor, ce_norm: bool = True) -> torch.Tensor:
    """MSE for a scalar head, two-hot cross-entropy for a distributional one. With
    `ce_norm`, the cross-entropy is divided by log(bins): uniform logits (a fresh
    head) then score exactly 1, the scale of the MSE of +-1 returns."""
    if value_logits is None:
        return F.mse_loss(values, targets)
    target_dist = two_hot(targets, model.value_support)
    ce = -(target_dist * F.log_softmax(value_logits, dim=-1)).sum(-1).mean()
    return ce / math.log(value_logits.shape[-1]) if ce_norm else ce


def kl_early_stop(minibatch_kls: List[float], target_kl: float, mode: str = "last") -> bool:
    """Whether to stop the PPO epochs after an epoch with these minibatch approx KLs."""
    if not target_kl or not minibatch_kls:
        return False
    kl = float(np.mean(minibatch_kls)) if mode == "mean" else minibatch_kls[-1]
    return kl > target_kl


def compute_gae(episodes: List[List[dict]], gamma: float, lam: float):
    """Flatten episodes into a buffer with advantages and returns (and the
    rollout values, for explained variance)."""
    obs, actions, logps, advs, rets, vals = [], [], [], [], [], []
    for ep in episodes:
        T = len(ep)
        values = np.array([s["value"] for s in ep] + [0.0], dtype=np.float32)
        rewards = np.array([s["reward"] for s in ep], dtype=np.float32)
        adv = np.zeros(T, dtype=np.float32)
        gae = 0.0
        for t in reversed(range(T)):
            delta = rewards[t] + gamma * values[t + 1] - values[t]  # terminal after T-1
            gae = delta + gamma * lam * gae
            adv[t] = gae
        obs += [s["obs"] for s in ep]
        actions += [s["action"] for s in ep]
        logps += [s["logp"] for s in ep]
        advs.append(adv)
        rets.append(adv + values[:-1])
        vals.append(values[:-1])
    return (
        obs,
        np.asarray(actions, dtype=np.int64),
        np.asarray(logps, dtype=np.float32),
        np.concatenate(advs),
        np.concatenate(rets),
        np.concatenate(vals),
    )


def explained_variance(values: np.ndarray, returns: np.ndarray) -> float:
    var = float(np.var(returns))
    return float("nan") if var == 0 else 1.0 - float(np.var(returns - values)) / var


def ppo_update(model, optimizer, episodes, cfg: PPOConfig, device,
               ent_coef: Optional[float] = None) -> Dict[str, float]:
    """One PPO update. The optimizer's LR is set by the caller (schedules);
    `ent_coef` overrides cfg.ent_coef when annealed."""
    model.train()
    ent_coef = cfg.ent_coef if ent_coef is None else ent_coef
    obs, actions, old_logp, adv, ret, old_values = compute_gae(episodes, cfg.gamma, cfg.gae_lambda)
    # Normalise over the whole buffer, not per minibatch: a 1-sample tail
    # minibatch has a NaN std, which turns every weight into NaN.
    adv = (adv - adv.mean()) / (adv.std() + 1e-8)
    n = len(obs)
    batch = collate(obs)
    stats = {"pi_loss": 0.0, "v_loss": 0.0, "v_mse": 0.0, "entropy": 0.0, "kl": 0.0,
             "clipfrac": 0.0}
    n_updates = 0
    epochs_run = 0

    for _ in range(cfg.epochs):
        perm = np.random.permutation(n)
        epoch_kls: List[float] = []
        for start in range(0, n, cfg.minibatch):
            idx = perm[start : start + cfg.minibatch]
            mb = to_tensors({k: v[idx] for k, v in batch.items()}, device)
            a = torch.from_numpy(actions[idx]).to(device)
            olp = torch.from_numpy(old_logp[idx]).to(device)
            A = torch.from_numpy(adv[idx]).to(device)
            R = torch.from_numpy(ret[idx]).to(device)

            logits, values, value_logits = model.forward_train(mb)
            dist = torch.distributions.Categorical(logits=logits)
            logp = dist.log_prob(a)
            ratio = torch.exp(logp - olp)
            pi_loss = -torch.min(ratio * A, ratio.clamp(1 - cfg.clip, 1 + cfg.clip) * A).mean()
            v_loss = value_loss(model, values, value_logits, R, cfg.value_ce_norm)
            entropy = dist.entropy().mean()
            loss = pi_loss + cfg.vf_coef * v_loss - ent_coef * entropy
            if not torch.isfinite(loss):  # never let one bad minibatch poison the weights
                stats["skipped"] = stats.get("skipped", 0) + 1
                continue

            optimizer.zero_grad(set_to_none=True)
            loss.backward()
            grad_norm = torch.nn.utils.clip_grad_norm_(model.parameters(), cfg.max_grad_norm)
            if not torch.isfinite(grad_norm):
                # Never step on a NaN/inf gradient: one bad minibatch would ruin the model.
                optimizer.zero_grad(set_to_none=True)
                stats["skipped"] = stats.get("skipped", 0) + 1
                continue
            optimizer.step()

            with torch.no_grad():
                approx_kl = ((ratio - 1) - torch.log(ratio)).mean().item()
                epoch_kls.append(approx_kl)
                stats["pi_loss"] += pi_loss.item()
                stats["v_loss"] += v_loss.item()
                stats["v_mse"] += F.mse_loss(values, R).item()
                stats["entropy"] += entropy.item()
                stats["kl"] += approx_kl
                stats["clipfrac"] += ((ratio - 1).abs() > cfg.clip).float().mean().item()
            n_updates += 1
        epochs_run += 1
        if kl_early_stop(epoch_kls, cfg.target_kl, cfg.kl_stop):
            break

    skipped = stats.pop("skipped", 0)
    out = {k: v / max(n_updates, 1) for k, v in stats.items()}
    out["samples"] = n
    out["skipped"] = skipped
    out["epochs"] = epochs_run
    out["explained_var"] = explained_variance(old_values, ret)
    return out


def distill_train(model, optimizer, dataset, epochs: int, minibatch: int, device,
                  vf_coef: float = 0.0, value_target: str = "teacher",
                  seed: int = 0, log_prefix: str = "[bc]",
                  value_ce_norm: bool = True) -> Dict[str, float]:
    """Trains the policy on the stored teacher distributions (cross-entropy to
    the teacher's softmax, i.e. KL(teacher || student) up to a constant), plus
    `vf_coef` x value loss toward the teacher's value or the battle's return.

    With one-hot "distributions" (the heuristic teacher) this is plain behavioural
    cloning. `dataset` is a dataset.ShardDataset, streamed shard by shard.
    """
    rng = np.random.default_rng(seed)
    value_key = {"teacher": "value", "return": "ret"}[value_target]
    last: Dict[str, float] = {}
    for ep in range(epochs):
        model.train()
        tot = {"policy": 0.0, "value": 0.0}
        cnt = 0
        for mb in dataset.batches("train", minibatch, rng=rng):
            obs = to_tensors(mb["obs"], device)
            probs = torch.from_numpy(mb["probs"]).to(device)
            logits, values, value_logits = model.forward_train(obs)
            pol = -(probs * F.log_softmax(logits, dim=-1)).sum(-1).mean()
            loss = pol
            if vf_coef:
                target = torch.from_numpy(mb[value_key]).to(device)
                v = value_loss(model, values, value_logits, target, value_ce_norm)
                loss = loss + vf_coef * v
                tot["value"] += v.item() * len(probs)
            optimizer.zero_grad(set_to_none=True)
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step()
            tot["policy"] += pol.item() * len(probs)
            cnt += len(probs)

        val = evaluate_distill(model, dataset, minibatch, device, value_key)
        last = {"train_policy": tot["policy"] / max(cnt, 1),
                "train_value": tot["value"] / max(cnt, 1), **val}
        print(f"{log_prefix} epoch {ep + 1}/{epochs}  train CE {last['train_policy']:.4f}"
              + (f"  value {last['train_value']:.4f}" if vf_coef else "")
              + f"  | val CE {val['val_policy']:.4f}  KL {val['val_kl']:.4f}  "
                f"agree {val['val_agree']:.3f}  value mse {val['val_value_mse']:.4f}")
    return last


@torch.no_grad()
def evaluate_distill(model, dataset, minibatch: int, device, value_key: str) -> Dict[str, float]:
    """Validation split: CE and KL to the teacher, argmax agreement with the
    teacher's argmax, and MSE of the scalar value to its target."""
    model.eval()
    ce = kl = agree = vmse = 0.0
    n = 0
    for mb in dataset.batches("val", minibatch):
        obs = to_tensors(mb["obs"], device)
        probs = torch.from_numpy(mb["probs"]).to(device)
        logits, values = model(obs)
        logq = F.log_softmax(logits, dim=-1)
        ce += -(probs * logq).sum().item()
        kl += (torch.xlogy(probs, probs) - probs * logq).sum().item()
        agree += (logits.argmax(-1) == probs.argmax(-1)).sum().item()
        vmse += ((values - torch.from_numpy(mb[value_key]).to(device)) ** 2).sum().item()
        n += len(probs)
    n = max(n, 1)
    return {"val_policy": ce / n, "val_kl": kl / n, "val_agree": agree / n, "val_value_mse": vmse / n}
