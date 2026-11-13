"""
Offline RL on human replays (human_data.py's dataset), in the spirit of Metamon (Grigsby et al.,
RLC 2025: offline RL on human replays beat imitating them).

    train.py offline --data data/human-v21 --size wide-medium --algo bc
    train.py offline --data data/human-v21 --size wide-medium --algo awbc --weight exp
    train.py offline --data data/human-v21 --size wide-medium --algo q
    train.py offline-eval --ckpt checkpoints/human/offline.pt --data data/human-v21

Algorithms (`OfflineConfig.algo`), each training the value head on the dataset's returns (the
humans' value: discounted, faint-shaped as in PPO, `vf_coef` x the usual value loss):
 - bc:   the actor loss is -log pi(a|s) on the human's action (behavioural cloning);
 - awbc: advantage-weighted BC (AWR / CRR): -w(A) log pi(a|s), A by GAE (gamma, lambda) along
         each human trajectory with the value head as the critic, refreshed by an ordered pass
         over the training set at the start of every epoch; w = 1[A > 0] ("binary") or
         min(exp(A / beta), max_weight) ("exp", A divided by its standard deviation first);
         the first `warmup_epochs` are plain BC, while the critic learns;
 - q:    the same weights, with A = Q(s, a) - sum_a' pi(a'|s) Q(s, a') from a per-action Q head
         used only in training (model.with_q_head, never exported), trained by TD toward
         r + gamma V(s') (V from the ordered pass).
Unlabelled steps (a choice that maps to no legal action, an unanswered last request) train the
value head and carry the returns, never the actor.

The labels are one-hot (one human choice per state): a big model memorizes them. Every epoch is
scored on the validation battles (NLL of the human choices), the best epoch is kept
(`offline.pt`) and training stops after `patience` epochs without improving.

Metrics (`offline_metrics`, on the test battles: the most recent ones): top-1 agreement and NLL
of the human choices (players rated `min_rating`+, and all), the value head's AUC at telling won
from lost battles' decisions, and its calibration (mean value vs mean return and win rate, by
value bin).
"""

from __future__ import annotations

import json
import math
import time
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Dict

import numpy as np
import torch
import torch.nn.functional as F

from dataset import ShardDataset
from model import to_tensors
from ppo import value_loss


@dataclass
class OfflineConfig:
    algo: str = "bc"              # bc | awbc | q
    weight: str = "exp"           # awbc / q: binary | exp
    beta: float = 1.0             # exp weights: exp(A / beta), A in standard deviations
    max_weight: float = 20.0
    warmup_epochs: int = 1        # awbc / q: plain BC epochs first, while the critic learns
    gamma: float = 0.995
    lam: float = 0.95
    vf_coef: float = 0.5
    q_coef: float = 0.5
    value_ce_norm: bool = True
    epochs: int = 10
    minibatch: int = 512
    lr: float = 3e-4
    lr_min_ratio: float = 0.1     # cosine over the run's steps, after `warmup_steps`
    warmup_steps: int = 200
    weight_decay: float = 1e-2
    max_grad_norm: float = 1.0
    patience: int = 2             # epochs without a better validation NLL before stopping
    min_rating: int = 1700        # the metrics' "strong players" threshold
    train_min_rating: int = 0     # train the actor only on players rated this or more


class HumanData:
    """A human_data.py dataset: its three splits (ShardDatasets, read whole: val_frac 0) and the
    meta (encoder flags, set priors, gamma, faint reward)."""

    def __init__(self, root: Path | str, buffer_shards: int = 4):
        self.root = Path(root)
        self.meta = json.loads((self.root / "meta.json").read_text())
        self.splits = {name: ShardDataset(self.root / name, val_frac=0.0, buffer_shards=buffer_shards)
                       for name in ("train", "val", "test") if (self.root / name / "meta.json").exists()}

    def __getitem__(self, split: str) -> ShardDataset:
        return self.splits[split]

    @property
    def obs_layout(self):
        return self.splits["train"].obs_layout


# --------------------------------------------------------------------------
# Advantages: an ordered pass for V (and Q), then GAE along each trajectory
# --------------------------------------------------------------------------
@torch.no_grad()
def ordered_pass(model, data: ShardDataset, device, batch: int = 1024, q: bool = False
                 ) -> Dict[str, np.ndarray]:
    """V(s) for every row of `data` in global row order, and with `q` the Q head's estimate of
    A(s, a) = Q(s, a) - sum pi Q(s, .) at the row's action (0 when unlabelled)."""
    model.eval()
    n = len(data)
    values = np.zeros(n, dtype=np.float32)
    q_adv = np.zeros(n, dtype=np.float32) if q else None
    for mb in data.batches("train", batch):
        obs = to_tensors(mb["obs"], device)
        rows = mb["row"]
        if q:
            logits, v, _, qv = model.forward_train(obs, q=True)
            probs = torch.softmax(logits, -1)
            baseline = (probs * qv).sum(-1)
            action = torch.from_numpy(mb["action"]).to(device)
            taken = qv.gather(1, action.clamp_min(0).unsqueeze(1)).squeeze(1)
            q_adv[rows] = ((taken - baseline) * (action >= 0)).float().cpu().numpy()
        else:
            _, v = model(obs)
        values[rows] = v.float().cpu().numpy()
    return {"value": values, "q_adv": q_adv}


def trajectory_order(traj: np.ndarray, step: np.ndarray) -> np.ndarray:
    """Row indices sorted by (trajectory, step)."""
    return np.lexsort((step, traj))


def gae(values: np.ndarray, rewards: np.ndarray, dones: np.ndarray, traj: np.ndarray,
        step: np.ndarray, gamma: float, lam: float) -> Dict[str, np.ndarray]:
    """GAE advantages and the next state's value (0 after a trajectory's last step) of every
    row, rows in any order (`traj`, `step` put them back in trajectory order). A trajectory
    ends at its `done` step, or where the next row is another trajectory or not the next step."""
    order = trajectory_order(traj, step)
    v, r, d = values[order], rewards[order], dones[order].astype(bool)
    t, s = traj[order], step[order]
    last = d.copy()
    last[:-1] |= (t[1:] != t[:-1]) | (s[1:] != s[:-1] + 1)
    last[-1] = True
    next_v = np.zeros_like(v)
    next_v[:-1] = v[1:]
    next_v[last] = 0.0
    delta = r + gamma * next_v - v
    adv = np.zeros_like(v)
    acc = 0.0
    for i in range(len(v) - 1, -1, -1):
        acc = delta[i] + (0.0 if last[i] else gamma * lam * acc)
        adv[i] = acc
    out_adv, out_next = np.zeros_like(adv), np.zeros_like(next_v)
    out_adv[order], out_next[order] = adv, next_v
    return {"adv": out_adv, "next_value": out_next}


def advantage_weights(adv: np.ndarray, labelled: np.ndarray, cfg: OfflineConfig) -> np.ndarray:
    """w(A) per row: 1[A > 0], or min(exp(A / std / beta), max_weight); 0 when unlabelled."""
    if cfg.weight == "binary":
        w = (adv > 0).astype(np.float32)
    else:
        std = float(adv[labelled].std()) if labelled.any() else 1.0
        w = np.minimum(np.exp(np.clip(adv / max(std, 1e-6) / cfg.beta, -50, 50)), cfg.max_weight)
    return (w * labelled).astype(np.float32)


# --------------------------------------------------------------------------
# Metrics
# --------------------------------------------------------------------------
def auc(scores: np.ndarray, labels: np.ndarray) -> float:
    """P(a positive scores above a negative), ties half (Mann-Whitney U / (pos x neg))."""
    labels = labels.astype(bool)
    pos, neg = int(labels.sum()), int((~labels).sum())
    if not pos or not neg:
        return float("nan")
    order = np.argsort(scores, kind="mergesort")
    sorted_scores = scores[order]
    ranks = np.empty(len(scores), dtype=np.float64)
    # average ranks over ties
    _, first, counts = np.unique(sorted_scores, return_index=True, return_counts=True)
    avg = first + (counts - 1) / 2.0 + 1.0
    ranks[order] = np.repeat(avg, counts)
    return float((ranks[labels].sum() - pos * (pos + 1) / 2) / (pos * neg))


@torch.no_grad()
def predict(model, data: ShardDataset, device, batch: int = 1024) -> Dict[str, np.ndarray]:
    """The model's log-probabilities of the human's action, argmax, value, and each row's
    action, label, rating, result and return, over a whole split (row order)."""
    if hasattr(model, "eval"):
        model.eval()
    keys = ("logp", "argmax", "value", "entropy", "action", "label_ok", "rating", "result", "ret", "turn")
    out = {k: [] for k in keys}
    for mb in data.batches("train", batch):
        logits, values = model(to_tensors(mb["obs"], device))
        logq = F.log_softmax(logits.float(), -1)
        action = torch.from_numpy(mb["action"]).to(logq.device)
        out["logp"].append(logq.gather(1, action.clamp_min(0).unsqueeze(1)).squeeze(1).cpu().numpy())
        out["argmax"].append(logits.argmax(-1).cpu().numpy())
        out["entropy"].append(-(logq.exp() * logq.clamp_min(-1e4)).sum(-1).cpu().numpy())
        out["value"].append(values.float().cpu().numpy())
        for k in ("action", "label_ok", "rating", "result", "ret", "turn"):
            out[k].append(mb[k] if k in mb else np.zeros(len(mb["action"])))
    return {k: np.concatenate(v) if v else np.zeros(0) for k, v in out.items()}


def policy_metrics(p: Dict[str, np.ndarray], min_rating: int = 0) -> Dict[str, float]:
    sel = (p["label_ok"] > 0) & (p["action"] >= 0) & (p["rating"] >= min_rating)
    n = int(sel.sum())
    if not n:
        return {"n": 0, "nll": float("nan"), "top1": float("nan")}
    return {"n": n, "nll": float(-p["logp"][sel].mean()),
            "top1": float((p["argmax"][sel] == p["action"][sel]).mean()),
            "entropy": float(p["entropy"][sel].mean())}


def value_metrics(p: Dict[str, np.ndarray], bins=(-1.0, -0.5, -0.25, 0.0, 0.25, 0.5, 1.0)) -> Dict[str, object]:
    """AUC of the value at telling won from lost battles' decisions (ties out), MSE to the
    return, and the calibration: per value bin, the mean value, return and win rate."""
    decided = p["result"] != 0
    out = {"auc": auc(p["value"][decided], p["result"][decided] > 0),
           "mse_return": float(((p["value"] - p["ret"]) ** 2).mean()) if len(p["value"]) else float("nan"),
           "mean_value": float(p["value"].mean()) if len(p["value"]) else float("nan"),
           "mean_return": float(p["ret"].mean()) if len(p["ret"]) else float("nan")}
    edges = [-np.inf, *bins, np.inf]
    calibration = []
    for lo, hi in zip(edges[:-1], edges[1:]):
        sel = (p["value"] >= lo) & (p["value"] < hi)
        if sel.any():
            calibration.append({"bin": [None if math.isinf(lo) else lo, None if math.isinf(hi) else hi],
                                "n": int(sel.sum()), "value": float(p["value"][sel].mean()),
                                "return": float(p["ret"][sel].mean()),
                                "win_rate": float((p["result"][sel] > 0).mean())})
    out["calibration"] = calibration
    return out


def offline_metrics(model, data: ShardDataset, device, min_rating: int = 1700) -> Dict[str, object]:
    p = predict(model, data, device)
    return {"strong": policy_metrics(p, min_rating), "all": policy_metrics(p, 0),
            "value": value_metrics(p), "min_rating": min_rating}


def describe_metrics(m: Dict[str, object]) -> str:
    s, a, v = m["strong"], m["all"], m["value"]
    lines = [f"  {m['min_rating']}+ players: top-1 {s['top1']:.3f}  NLL {s['nll']:.3f}  ({s['n']} choices)",
             f"  all players:   top-1 {a['top1']:.3f}  NLL {a['nll']:.3f}  ({a['n']} choices)",
             f"  value: AUC {v['auc']:.3f}  MSE to return {v['mse_return']:.3f}  "
             f"mean value {v['mean_value']:+.3f} vs return {v['mean_return']:+.3f}",
             "  calibration (value bin: n, mean value, mean return, win rate):"]
    for c in v["calibration"]:
        lo, hi = c["bin"]
        lines.append(f"    [{'-inf' if lo is None else f'{lo:+.2f}'}, {'inf' if hi is None else f'{hi:+.2f}'}): "
                     f"{c['n']:6d}  {c['value']:+.3f}  {c['return']:+.3f}  {c['win_rate']:.3f}")
    return "\n".join(lines)


# --------------------------------------------------------------------------
# Training
# --------------------------------------------------------------------------
def offline_train(model, data: HumanData, cfg: OfflineConfig, device, out_dir: Path, save,
                  seed: int = 0, log=print) -> Dict[str, object]:
    """Trains `model` on data["train"], early-stopping on data["val"]'s NLL. `save(path, model,
    **extra)` writes a checkpoint; the best epoch goes to out_dir/offline.pt, the last to
    out_dir/offline_last.pt. Returns the history and the best epoch's validation metrics."""
    rng = np.random.default_rng(seed)
    train, val = data["train"], data["val"]
    n = len(train)
    cols = {k: train.column(k) for k in ("reward", "done", "traj", "step", "label_ok", "rating", "action")}
    labelled = (cols["label_ok"] > 0) & (cols["action"] >= 0) & (cols["rating"] >= cfg.train_min_rating)
    use_q = cfg.algo == "q"
    opt = torch.optim.AdamW(model.parameters(), lr=cfg.lr, weight_decay=cfg.weight_decay)
    steps_per_epoch = math.ceil(n / cfg.minibatch)
    total = max(1, steps_per_epoch * cfg.epochs)

    def lr_at(step: int) -> float:
        if step < cfg.warmup_steps:
            return cfg.lr * (step + 1) / cfg.warmup_steps
        progress = min(1.0, (step - cfg.warmup_steps) / max(1, total - cfg.warmup_steps))
        return cfg.lr * (cfg.lr_min_ratio + (1 - cfg.lr_min_ratio) * 0.5 * (1 + math.cos(math.pi * progress)))

    history, best, best_epoch, bad, step = [], math.inf, 0, 0, 0
    log(f"[offline] {cfg.algo}: {n} training states ({int(labelled.sum())} labelled), "
        f"{len(val)} validation, {steps_per_epoch} steps per epoch")
    for epoch in range(1, cfg.epochs + 1):
        t0 = time.time()
        weights = next_value = None
        stats = {"actor": 0.0, "value": 0.0, "q": 0.0, "w_mean": float("nan"), "w_pos": float("nan")}
        if cfg.algo in ("awbc", "q") and epoch > cfg.warmup_epochs:
            passed = ordered_pass(model, train, device, q=use_q)
            g = gae(passed["value"], cols["reward"], cols["done"], cols["traj"], cols["step"], cfg.gamma, cfg.lam)
            adv = passed["q_adv"] if use_q else g["adv"]
            weights = advantage_weights(adv, labelled, cfg)
            next_value = g["next_value"]
            stats["w_mean"] = float(weights[labelled].mean())
            stats["w_pos"] = float((adv[labelled] > 0).mean())
        elif use_q:
            passed = ordered_pass(model, train, device)
            next_value = gae(passed["value"], cols["reward"], cols["done"], cols["traj"], cols["step"],
                             cfg.gamma, cfg.lam)["next_value"]
        model.train()
        seen = 0
        for mb in train.batches("train", cfg.minibatch, rng=rng):
            for group in opt.param_groups:
                group["lr"] = lr_at(step)
            obs = to_tensors(mb["obs"], device)
            rows = mb["row"]
            if use_q:
                logits, values, value_logits, qv = model.forward_train(obs, q=True)
            else:
                logits, values, value_logits = model.forward_train(obs)
            action = torch.from_numpy(mb["action"]).to(device)
            lab = torch.from_numpy(labelled[rows]).to(device)
            w = torch.from_numpy(weights[rows]).to(device) if weights is not None else lab.float()
            logp = F.log_softmax(logits, -1).gather(1, action.clamp_min(0).unsqueeze(1)).squeeze(1)
            actor = -(w * logp * lab).sum() / lab.sum().clamp_min(1)
            ret = torch.from_numpy(mb["ret"]).to(device)
            v_loss = value_loss(model, values, value_logits, ret, cfg.value_ce_norm)
            loss = actor + cfg.vf_coef * v_loss
            if use_q:
                target = torch.from_numpy(mb["reward"] + cfg.gamma * next_value[rows]).to(device)
                taken = qv.gather(1, action.clamp_min(0).unsqueeze(1)).squeeze(1)
                q_loss = (((taken - target) ** 2) * lab).sum() / lab.sum().clamp_min(1)
                loss = loss + cfg.q_coef * q_loss
                stats["q"] += q_loss.item() * len(rows)
            if not torch.isfinite(loss):
                continue
            opt.zero_grad(set_to_none=True)
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), cfg.max_grad_norm)
            opt.step()
            step += 1
            stats["actor"] += actor.item() * len(rows)
            stats["value"] += v_loss.item() * len(rows)
            seen += len(rows)
        for k in ("actor", "value", "q"):
            stats[k] /= max(seen, 1)
        p = predict(model, val, device)
        vm = {**policy_metrics(p, 0), "value_auc": value_metrics(p)["auc"]}
        row = {"epoch": epoch, "seconds": time.time() - t0, "lr": lr_at(step), **stats,
               **{f"val_{k}": v for k, v in vm.items()}}
        history.append(row)
        log(f"[offline] epoch {epoch}/{cfg.epochs} ({row['seconds']:.0f} s)  actor {stats['actor']:.4f}  "
            f"value {stats['value']:.4f}" + (f"  q {stats['q']:.4f}" if use_q else "")
            + (f"  w mean {stats['w_mean']:.2f} (A>0 {stats['w_pos']:.2f})" if weights is not None else "")
            + f"  | val NLL {vm['nll']:.4f}  top-1 {vm['top1']:.3f}  value AUC {vm['value_auc']:.3f}")
        extra = {"offline": {"config": asdict(cfg), "epoch": epoch, "val": vm, "data": str(data.root)}}
        save(out_dir / "offline_last.pt", model, **extra)
        if vm["nll"] < best:
            best, best_epoch, bad = vm["nll"], epoch, 0
            save(out_dir / "offline.pt", model, **extra)
            log(f"[offline] best so far -> {out_dir / 'offline.pt'}")
        else:
            bad += 1
            if bad >= cfg.patience:
                log(f"[offline] no better validation NLL for {bad} epochs: stopping (best epoch {best_epoch})")
                break
    return {"history": history, "best_epoch": best_epoch, "best_val_nll": best}


# --------------------------------------------------------------------------
# A released model (ONNX) as a policy: offline-eval and eval --vs-ckpt <model folder>
# --------------------------------------------------------------------------
class OnnxPolicy:
    """A released model's minipoke.onnx with its encoder (encoder_spec.json, set_priors.json),
    behaving like a frozen MiniPokeTransformer for inference: `cfg`, `priors` (so
    encoder_v2.model_encoder rebuilds its encoder), `__call__(obs tensors) -> (logits, value)`,
    and the no-ops rollout.frozen_copy calls."""

    def __init__(self, folder: Path | str, threads: int = 1):
        import onnxruntime as ort

        from make_fixtures import encoder_from_spec
        from model import ModelConfig
        self.folder = Path(folder)
        spec = json.loads((self.folder / "encoder_spec.json").read_text())
        self.encoder = encoder_from_spec(str(self.folder / "encoder_spec.json"))
        self.cfg = ModelConfig.from_dict(spec.get("model") or {"encoder_version": 1})
        self.priors = getattr(self.encoder, "priors", None)
        options = ort.SessionOptions()
        options.intra_op_num_threads = threads   # 1 in eval's worker processes
        self.threads = threads
        self.session = ort.InferenceSession(str(self.folder / "minipoke.onnx"), options,
                                            providers=["CPUExecutionProvider"])
        self.input_names = [i.name for i in self.session.get_inputs()]

    def __call__(self, obs: Dict[str, torch.Tensor]):
        feed = {}
        for name in self.input_names:
            value = obs[name]
            feed[name] = value.cpu().numpy() if isinstance(value, torch.Tensor) else np.asarray(value)
        logits, value = self.session.run(None, feed)
        return torch.from_numpy(logits), torch.from_numpy(value)

    def eval(self):
        return self

    def to(self, device):
        return self

    def parameters(self):
        return iter(())

    def named_parameters(self):
        return iter(())

    def __deepcopy__(self, memo):
        return OnnxPolicy(self.folder, self.threads)

    def describe(self) -> str:
        return f"ONNX model {self.folder.name} ({(self.folder / 'minipoke.onnx').stat().st_size / 2**20:.0f} MiB)"


def is_onnx_folder(path: Path | str) -> bool:
    path = Path(path)
    return path.is_dir() and (path / "minipoke.onnx").exists() and (path / "encoder_spec.json").exists()
