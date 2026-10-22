"""PPO and behavioural-cloning updates. Runs on the training device (MPS on an M3)."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Dict, List

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


def compute_gae(episodes: List[List[dict]], gamma: float, lam: float):
    """Flatten episodes into a buffer with advantages and returns."""
    obs, actions, logps, advs, rets = [], [], [], [], []
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
    return (
        obs,
        np.asarray(actions, dtype=np.int64),
        np.asarray(logps, dtype=np.float32),
        np.concatenate(advs),
        np.concatenate(rets),
    )


def ppo_update(model, optimizer, episodes, cfg: PPOConfig, device) -> Dict[str, float]:
    model.train()
    obs, actions, old_logp, adv, ret = compute_gae(episodes, cfg.gamma, cfg.gae_lambda)
    # Normalise over the whole buffer, not per minibatch: a 1-sample tail
    # minibatch has a NaN std, which turns every weight into NaN.
    adv = (adv - adv.mean()) / (adv.std() + 1e-8)
    n = len(obs)
    batch = collate(obs)
    stats = {"pi_loss": 0.0, "v_loss": 0.0, "entropy": 0.0, "kl": 0.0, "clipfrac": 0.0}
    n_updates = 0

    for _ in range(cfg.epochs):
        perm = np.random.permutation(n)
        approx_kl = 0.0
        for start in range(0, n, cfg.minibatch):
            idx = perm[start : start + cfg.minibatch]
            mb = to_tensors({k: v[idx] for k, v in batch.items()}, device)
            a = torch.from_numpy(actions[idx]).to(device)
            olp = torch.from_numpy(old_logp[idx]).to(device)
            A = torch.from_numpy(adv[idx]).to(device)
            R = torch.from_numpy(ret[idx]).to(device)

            logits, values = model(mb)
            dist = torch.distributions.Categorical(logits=logits)
            logp = dist.log_prob(a)
            ratio = torch.exp(logp - olp)
            pi_loss = -torch.min(ratio * A, ratio.clamp(1 - cfg.clip, 1 + cfg.clip) * A).mean()
            v_loss = F.mse_loss(values, R)
            entropy = dist.entropy().mean()
            loss = pi_loss + cfg.vf_coef * v_loss - cfg.ent_coef * entropy

            optimizer.zero_grad(set_to_none=True)
            loss.backward()
            grad_norm = torch.nn.utils.clip_grad_norm_(model.parameters(), cfg.max_grad_norm)
            if not torch.isfinite(grad_norm):
                # Never step on a NaN/inf gradient: one bad minibatch would ruin the model.
                print("[ppo] warning: non-finite gradient, skipped one minibatch")
                continue
            optimizer.step()

            with torch.no_grad():
                approx_kl = ((ratio - 1) - torch.log(ratio)).mean().item()
                stats["pi_loss"] += pi_loss.item()
                stats["v_loss"] += v_loss.item()
                stats["entropy"] += entropy.item()
                stats["kl"] += approx_kl
                stats["clipfrac"] += ((ratio - 1).abs() > cfg.clip).float().mean().item()
            n_updates += 1
        if cfg.target_kl and approx_kl > cfg.target_kl:
            break

    out = {k: v / max(n_updates, 1) for k, v in stats.items()}
    out["samples"] = n
    return out


def bc_train(model, optimizer, dataset, epochs: int, minibatch: int, device,
             val_frac: float = 0.05) -> None:
    """Cross-entropy imitation of the heuristic player's actions."""
    obs = [o for o, _ in dataset]
    acts = np.asarray([a for _, a in dataset], dtype=np.int64)
    batch = collate(obs)
    n = len(obs)
    perm = np.random.permutation(n)
    n_val = max(1, int(n * val_frac))
    val_idx, train_idx = perm[:n_val], perm[n_val:]

    for ep in range(epochs):
        model.train()
        np.random.shuffle(train_idx)
        tot, cnt = 0.0, 0
        for start in range(0, len(train_idx), minibatch):
            idx = train_idx[start : start + minibatch]
            mb = to_tensors({k: v[idx] for k, v in batch.items()}, device)
            a = torch.from_numpy(acts[idx]).to(device)
            logits, _ = model(mb)
            loss = F.cross_entropy(logits, a)
            optimizer.zero_grad(set_to_none=True)
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step()
            tot += loss.item() * len(idx)
            cnt += len(idx)

        model.eval()
        with torch.no_grad():
            correct = 0
            for start in range(0, n_val, minibatch):
                idx = val_idx[start : start + minibatch]
                mb = to_tensors({k: v[idx] for k, v in batch.items()}, device)
                logits, _ = model(mb)
                correct += (logits.argmax(-1).cpu().numpy() == acts[idx]).sum()
        print(f"[bc] epoch {ep + 1}/{epochs}  loss {tot / cnt:.4f}  "
              f"val acc {correct / n_val:.3f}")
