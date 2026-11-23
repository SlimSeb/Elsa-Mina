"""
Offline RL on human replays (human_data.py's dataset), in the spirit of Metamon (Grigsby et al.,
RLC 2025: offline RL on human replays beat imitating them).

    train.py offline --data data/human-v21 --size wide-medium --algo bc
    train.py offline --data data/human-v21 --size wide-medium --algo awbc --weight exp
    train.py offline-eval --ckpt checkpoints/human/offline.pt --data data/human-v21

Algorithms (`OfflineConfig.algo`), each training the value head on the dataset's returns (the
humans' value: +-1 at the end, 0.1 per faint difference, discounted; `vf_coef` x the value loss):
 - bc:   the actor loss is -log pi(a|s) on the human's action (behavioural cloning);
 - awbc: advantage-weighted BC (AWR / CRR): -w(A) log pi(a|s), A by GAE (gamma, lambda) along
         each human trajectory with the value head as the critic, refreshed by an ordered pass
         over the training set at the start of every epoch; w = 1[A > 0] ("binary") or
         min(exp(A / beta), max_weight) ("exp", A divided by its standard deviation first);
         the first `warmup_epochs` are plain BC, while the critic learns.
Unlabelled steps (a choice that maps to no legal action, an unanswered last request) train the
value head and carry the returns, never the actor.

The labels are one-hot (one human choice per state): a big model memorizes them. Every epoch is
scored on the validation battles (NLL of the human choices), the best epoch is kept
(`offline.pt`) and training stops after `patience` epochs without improving.

Metrics (`offline_metrics`, on the test battles: the most recent ones): top-1 agreement and NLL
of the human choices (players rated `min_rating`+, and all), the value head's AUC at telling won
from lost battles' decisions, and its calibration (mean value vs mean return and win rate, by
value bin).

Opponent heads (a model with `opp_heads`, a dataset with opp_targets.py's fields): with
`opp_coef` > 0, the loss adds opp_coef x the mean of six cross-entropies, each divided by the log
of its number of classes (about 1 at init, like the value loss): the opponent's choice, whether
it terastallizes, and each revealed opposing Pokémon's item, ability, tera type and moves.
`opp_metrics` measures them (top-1 on what our view has not seen yet).
"""

from __future__ import annotations

import json
import math
import time
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Dict, Optional

import numpy as np
import torch
import torch.nn.functional as F

from dataset import ShardDataset
from encoder import N_SLOTS, UNK_ID
from model import to_tensors
from opp_targets import OPP_EXTRA

ALGOS = ("bc", "awbc")


@dataclass
class OfflineConfig:
    algo: str = "bc"              # bc | awbc
    weight: str = "exp"           # awbc: binary | exp
    beta: float = 1.0             # exp weights: exp(A / beta), A in standard deviations
    max_weight: float = 20.0
    warmup_epochs: int = 1        # awbc: plain BC epochs first, while the critic learns
    gamma: float = 0.995
    lam: float = 0.95
    vf_coef: float = 0.5
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
    opp_coef: float = 0.0         # x the opponent heads' loss (0: not trained)


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
# The value loss: MSE for a scalar head, two-hot cross-entropy for a distributional one
# --------------------------------------------------------------------------
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


# --------------------------------------------------------------------------
# The opponent heads' loss
# --------------------------------------------------------------------------
def has_opp_heads(model) -> bool:
    return bool(getattr(getattr(model, "cfg", None), "opp_heads", False))


def has_opp_targets(data: ShardDataset) -> bool:
    return all(k in data.extra for k in OPP_EXTRA)


def _masked_mean(x: torch.Tensor, valid: torch.Tensor):
    """(mean of x where valid, 0 when none; 1 if any is valid else 0), without a device sync."""
    v = valid.to(x.dtype)
    n = v.sum()
    return (x * v).sum() / n.clamp_min(1.0), (n > 0).to(x.dtype)


def _target_logp(logits: torch.Tensor, target: torch.Tensor) -> torch.Tensor:
    """log_softmax(logits) at `target` (any shape of indices on the last axis; -1 reads 0)."""
    logq = F.log_softmax(logits.float(), dim=-1)
    if target.dim() == logits.dim() - 1:
        return logq.gather(-1, target.clamp_min(0).unsqueeze(-1)).squeeze(-1)
    return logq.gather(-1, target.clamp_min(0))


def opp_targets_of(mb: Dict[str, np.ndarray], device) -> Dict[str, torch.Tensor]:
    return {k: torch.from_numpy(mb[k]).to(device) for k in OPP_EXTRA}


def opp_loss(out: Dict[str, torch.Tensor], targets: Dict[str, torch.Tensor]):
    """The mean of the six opponent terms with targets in the batch, each cross-entropy divided by
    log(classes): the choice (rows with a target in a class our view allows), tera (BCE / log 2),
    item, ability, tera type (revealed slots), moves (the mean -log p of every real move id).
    Returns it and the terms."""
    targets = {k: v.long() for k, v in targets.items()}
    act = targets["opp_act"]
    allowed = out["opp_act_mask"].gather(1, act.clamp_min(0).unsqueeze(1)).squeeze(1)
    tera = targets["opp_tera"]
    bce = F.binary_cross_entropy_with_logits(out["opp_tera"].float(), tera.clamp_min(0).float(),
                                             reduction="none")
    moves = targets["opp_moves"]
    terms = {
        "act": (-_target_logp(out["opp_act"], act), (act >= 0) & allowed, out["opp_act"].shape[-1]),
        "tera": (bce, tera >= 0, 2),
        "item": (-_target_logp(out["opp_item"], targets["opp_item"]), targets["opp_item"] >= 0,
                 out["opp_item"].shape[-1]),
        "ability": (-_target_logp(out["opp_ability"], targets["opp_ability"]),
                    targets["opp_ability"] >= 0, out["opp_ability"].shape[-1]),
        "tera_type": (-_target_logp(out["opp_tera_type"], targets["opp_tera_type"]),
                      targets["opp_tera_type"] >= 0, out["opp_tera_type"].shape[-1]),
        "moves": (-_target_logp(out["opp_moves"], moves), moves > UNK_ID, out["opp_moves"].shape[-1]),
    }
    total, present, losses = 0.0, 0.0, {}
    for name, (nll, valid, classes) in terms.items():
        mean, has = _masked_mean(nll, valid)
        losses[name] = mean / math.log(classes)
        total, present = total + losses[name], present + has
    return total / present.clamp_min(1.0), losses


@torch.no_grad()
def opp_metrics(model, data: ShardDataset, device, batch: int = 1024) -> Dict[str, Dict[str, float]]:
    """The opponent heads on a split: the opponent's choice (top-1, NLL; rows with a target our
    view allows), tera accuracy, top-1 on the items and abilities our view has not seen yet
    (mon_ids UNK_ID), tera type top-1, and recall@4 of the moves not revealed yet (the 4 likeliest
    moves among those our view does not show)."""
    model.eval()
    sums = {k: 0.0 for k in ("choice_n", "choice_top1", "choice_nll", "tera_n", "tera_ok", "item_n",
                             "item_ok", "ability_n", "ability_ok", "tera_type_n", "tera_type_ok",
                             "moves_n", "moves_hit")}

    def add(key: str, x: torch.Tensor) -> None:
        sums[key] += float(x.sum())

    for mb in data.batches("train", batch):
        obs = to_tensors(mb["obs"], device)
        out = model.forward_train(obs, opp=True)[3]
        t = opp_targets_of(mb, device)
        act, logits = t["opp_act"], out["opp_act"].float()
        valid = (act >= 0) & out["opp_act_mask"].gather(1, act.clamp_min(0).unsqueeze(1)).squeeze(1)
        add("choice_n", valid)
        add("choice_top1", (logits.argmax(-1) == act) & valid)
        add("choice_nll", -_target_logp(logits, act) * valid)
        tera = t["opp_tera"]
        add("tera_n", tera >= 0)
        add("tera_ok", ((out["opp_tera"] > 0) == (tera > 0)) & (tera >= 0))
        theirs = obs["mon_ids"][:, N_SLOTS:]
        for name, column in (("item", 1), ("ability", 2)):
            target = t[f"opp_{name}"]
            hidden = (target >= 0) & (theirs[..., column] == UNK_ID)
            add(f"{name}_n", hidden)
            add(f"{name}_ok", (out[f"opp_{name}"].argmax(-1) == target) & hidden)
        target = t["opp_tera_type"]
        add("tera_type_n", target >= 0)
        add("tera_type_ok", (out["opp_tera_type"].argmax(-1) == target) & (target >= 0))
        known = obs["move_ids"][:, N_SLOTS:]                                    # [B, 6, 4]
        scores = out["opp_moves"].float().clone()
        scores[..., :UNK_ID + 1] = -math.inf
        scores.scatter_(-1, known, -math.inf)
        top = scores.topk(4, dim=-1).indices                                    # [B, 6, 4]
        real = t["opp_moves"]
        hidden = (real > UNK_ID) & ~(real.unsqueeze(-1) == known.unsqueeze(-2)).any(-1)
        add("moves_n", hidden)
        add("moves_hit", (real.unsqueeze(-1) == top.unsqueeze(-2)).any(-1) & hidden)

    def ratio(key: str, n: str) -> float:
        return sums[key] / sums[n] if sums[n] else float("nan")
    return {"choice": {"n": int(sums["choice_n"]), "top1": ratio("choice_top1", "choice_n"),
                       "nll": ratio("choice_nll", "choice_n")},
            "tera": {"n": int(sums["tera_n"]), "accuracy": ratio("tera_ok", "tera_n")},
            "item": {"n": int(sums["item_n"]), "top1": ratio("item_ok", "item_n")},
            "ability": {"n": int(sums["ability_n"]), "top1": ratio("ability_ok", "ability_n")},
            "tera_type": {"n": int(sums["tera_type_n"]), "top1": ratio("tera_type_ok", "tera_type_n")},
            "moves": {"n": int(sums["moves_n"]), "recall4": ratio("moves_hit", "moves_n")}}


def describe_opp_metrics(m: Dict[str, Dict[str, float]]) -> str:
    c = m["choice"]
    return "\n".join([
        f"  opponent's choice: top-1 {c['top1']:.3f}  NLL {c['nll']:.3f}  ({c['n']} choices); "
        f"tera accuracy {m['tera']['accuracy']:.3f} ({m['tera']['n']})",
        f"  not revealed yet: items top-1 {m['item']['top1']:.3f} ({m['item']['n']}), abilities top-1 "
        f"{m['ability']['top1']:.3f} ({m['ability']['n']}), moves recall@4 {m['moves']['recall4']:.3f} "
        f"({m['moves']['n']}); tera types top-1 {m['tera_type']['top1']:.3f} ({m['tera_type']['n']})"])


# --------------------------------------------------------------------------
# Advantages: an ordered pass for V, then GAE along each trajectory
# --------------------------------------------------------------------------
@torch.no_grad()
def ordered_pass(model, data: ShardDataset, device, batch: int = 1024) -> np.ndarray:
    """V(s) for every row of `data`, in global row order."""
    model.eval()
    values = np.zeros(len(data), dtype=np.float32)
    for mb in data.batches("train", batch):
        _, v = model(to_tensors(mb["obs"], device))
        values[mb["row"]] = v.float().cpu().numpy()
    return values


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
                  seed: int = 0, log=print, resume: Optional[dict] = None) -> Dict[str, object]:
    """Trains `model` on data["train"], early-stopping on data["val"]'s NLL. `save(path, model,
    optimizer, **extra)` writes a checkpoint; the best epoch goes to out_dir/offline.pt, the last
    to out_dir/offline_last.pt with the optimizer and what continuing the run needs (step, best
    epoch, history, shuffle state). `resume` (offline_last.pt's "offline" entry, plus its "opt")
    continues a stopped run after its last finished epoch, as if it had not stopped. Returns the
    history and the best epoch's validation metrics."""
    rng = np.random.default_rng(seed)
    train, val = data["train"], data["val"]
    n = len(train)
    cols = {k: train.column(k) for k in ("reward", "done", "traj", "step", "label_ok", "rating", "action")}
    labelled = (cols["label_ok"] > 0) & (cols["action"] >= 0) & (cols["rating"] >= cfg.train_min_rating)
    if cfg.algo not in ALGOS:
        raise ValueError(f"unknown offline algorithm {cfg.algo!r}: one of {ALGOS}")
    opp = cfg.opp_coef > 0
    if opp and not has_opp_heads(model):
        raise ValueError("opp_coef > 0 trains the opponent heads, and this model has none "
                         "(train.py offline --opp-heads adds them)")
    if opp and not (has_opp_targets(train) and has_opp_targets(val)):
        raise ValueError(f"opp_coef > 0 needs the opponent targets, and {data.root} has none: rebuild "
                         f"it with human_data.py (or train with --opp-coef 0)")
    opt = torch.optim.AdamW(model.parameters(), lr=cfg.lr, weight_decay=cfg.weight_decay)
    steps_per_epoch = math.ceil(n / cfg.minibatch)
    total = max(1, steps_per_epoch * cfg.epochs)

    history, best, best_epoch, bad, step = [], math.inf, 0, 0, 0
    first_epoch, rewarm_from = 1, None
    if resume:
        first_epoch = resume["epoch"] + 1
        state = resume.get("resume")
        if state:
            step, best, best_epoch, bad = state["step"], state["best"], state["best_epoch"], state["bad"]
            history = list(state["history"])
            rng.bit_generator.state = state["rng"]
        else:
            # Saved before runs could resume: the schedule's position from the epoch, the best
            # epoch from offline.pt, another shuffle
            step = resume["epoch"] * steps_per_epoch
            if (out_dir / "offline.pt").exists():
                saved_best = torch.load(out_dir / "offline.pt", map_location="cpu", weights_only=False)["offline"]
                best, best_epoch = saved_best["val"]["nll"], saved_best["epoch"]
                bad = resume["epoch"] - best_epoch
            rng = np.random.default_rng([seed, first_epoch])
        if resume.get("opt"):
            opt.load_state_dict(resume["opt"])
        else:
            rewarm_from = step
            log(f"[offline] no optimizer state saved: a new one, its LR warmed up again over "
                f"{cfg.warmup_steps} steps")
        log(f"[offline] resuming after epoch {resume['epoch']} (step {step}, best epoch {best_epoch}: "
            f"val NLL {best:.4f}, {bad} epochs without a better one)")

    def lr_at(step: int) -> float:
        if step < cfg.warmup_steps:
            return cfg.lr * (step + 1) / cfg.warmup_steps
        progress = min(1.0, (step - cfg.warmup_steps) / max(1, total - cfg.warmup_steps))
        lr = cfg.lr * (cfg.lr_min_ratio + (1 - cfg.lr_min_ratio) * 0.5 * (1 + math.cos(math.pi * progress)))
        if rewarm_from is not None and step < rewarm_from + cfg.warmup_steps:
            lr *= (step - rewarm_from + 1) / cfg.warmup_steps
        return lr

    last_epoch = cfg.epochs
    if bad >= cfg.patience:
        log(f"[offline] already stopped: no better validation NLL for {bad} epochs (best epoch {best_epoch})")
        last_epoch = first_epoch - 1
    log(f"[offline] {cfg.algo}: {n} training states ({int(labelled.sum())} labelled), "
        f"{len(val)} validation, {steps_per_epoch} steps per epoch")
    for epoch in range(first_epoch, last_epoch + 1):
        t0 = time.time()
        weights = None
        stats = {"actor": 0.0, "value": 0.0, "opp": 0.0 if opp else float("nan"),
                 "w_mean": float("nan"), "w_pos": float("nan")}
        if cfg.algo == "awbc" and epoch > cfg.warmup_epochs:
            values = ordered_pass(model, train, device)
            adv = gae(values, cols["reward"], cols["done"], cols["traj"], cols["step"], cfg.gamma, cfg.lam)["adv"]
            weights = advantage_weights(adv, labelled, cfg)
            stats["w_mean"] = float(weights[labelled].mean())
            stats["w_pos"] = float((adv[labelled] > 0).mean())
        model.train()
        seen = 0
        for mb in train.batches("train", cfg.minibatch, rng=rng):
            for group in opt.param_groups:
                group["lr"] = lr_at(step)
            obs = to_tensors(mb["obs"], device)
            rows = mb["row"]
            out = model.forward_train(obs, opp=True) if opp else model.forward_train(obs)
            logits, values, value_logits = out[:3]
            action = torch.from_numpy(mb["action"]).to(device)
            lab = torch.from_numpy(labelled[rows]).to(device)
            w = torch.from_numpy(weights[rows]).to(device) if weights is not None else lab.float()
            logp = F.log_softmax(logits, -1).gather(1, action.clamp_min(0).unsqueeze(1)).squeeze(1)
            actor = -(w * logp * lab).sum() / lab.sum().clamp_min(1)
            ret = torch.from_numpy(mb["ret"]).to(device)
            v_loss = value_loss(model, values, value_logits, ret, cfg.value_ce_norm)
            loss = actor + cfg.vf_coef * v_loss
            if opp:
                o_loss, _ = opp_loss(out[3], opp_targets_of(mb, device))
                loss = loss + cfg.opp_coef * o_loss
            if not torch.isfinite(loss):
                continue
            opt.zero_grad(set_to_none=True)
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), cfg.max_grad_norm)
            opt.step()
            step += 1
            stats["actor"] += actor.item() * len(rows)
            stats["value"] += v_loss.item() * len(rows)
            if opp:
                stats["opp"] += o_loss.item() * len(rows)
            seen += len(rows)
        for k in ("actor", "value") + (("opp",) if opp else ()):
            stats[k] /= max(seen, 1)
        p = predict(model, val, device)
        vm = {**policy_metrics(p, 0), "value_auc": value_metrics(p)["auc"]}
        if opp:
            vm["opp_top1"] = opp_metrics(model, val, device)["choice"]["top1"]
        row = {"epoch": epoch, "seconds": time.time() - t0, "lr": lr_at(step), **stats,
               **{f"val_{k}": v for k, v in vm.items()}}
        history.append(row)
        log(f"[offline] epoch {epoch}/{cfg.epochs} ({row['seconds']:.0f} s)  actor {stats['actor']:.4f}  "
            f"value {stats['value']:.4f}"
            + (f"  opp {stats['opp']:.4f}" if opp else "")
            + (f"  w mean {stats['w_mean']:.2f} (A>0 {stats['w_pos']:.2f})" if weights is not None else "")
            + f"  | val NLL {vm['nll']:.4f}  top-1 {vm['top1']:.3f}  value AUC {vm['value_auc']:.3f}"
            + (f"  opp top-1 {vm['opp_top1']:.3f}" if opp else ""))
        improved = vm["nll"] < best
        if improved:
            best, best_epoch, bad = vm["nll"], epoch, 0
        else:
            bad += 1
        info = {"config": asdict(cfg), "epoch": epoch, "val": vm, "data": str(data.root)}
        if improved:
            # Before offline_last.pt: a run resumed from it never finds an older best in offline.pt
            save(out_dir / "offline.pt", model, None, offline=info)
            log(f"[offline] best so far -> {out_dir / 'offline.pt'}")
        save(out_dir / "offline_last.pt", model, opt, offline={**info, "resume": {
            "step": step, "best": best, "best_epoch": best_epoch, "bad": bad, "history": history,
            "rng": rng.bit_generator.state}})
        if bad >= cfg.patience:
            log(f"[offline] no better validation NLL for {bad} epochs: stopping (best epoch {best_epoch})")
            break
    return {"history": history, "best_epoch": best_epoch, "best_val_nll": best}


# --------------------------------------------------------------------------
# A released model (ONNX) as a policy: offline-eval and eval --vs-ckpt <model folder>
# --------------------------------------------------------------------------
class OnnxPolicy:
    """A released model's gladion.onnx with its encoder (encoder_spec.json, set_priors.json),
    behaving like a frozen GladionTransformer for inference: `cfg`, `priors` (so
    encoder_v2.model_encoder rebuilds its encoder), `__call__(obs tensors) -> (logits, value)`,
    and the no-ops matches.frozen_copy calls."""

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
        self.session = ort.InferenceSession(str(self.folder / "gladion.onnx"), options,
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
        return f"ONNX model {self.folder.name} ({(self.folder / 'gladion.onnx').stat().st_size / 2**20:.0f} MiB)"


def is_onnx_folder(path: Path | str) -> bool:
    path = Path(path)
    return path.is_dir() and (path / "gladion.onnx").exists() and (path / "encoder_spec.json").exists()
