"""
Entry point.

    python train.py imitate --size medium --battles 3000 --epochs 8
    python train.py distill --teacher checkpoints/ppo_latest.pt --size medium --battles 3000
    python train.py ppo --init checkpoints/bc.pt --hours 24
    python train.py ppo --resume checkpoints/ppo_latest.pt
    python train.py ppo --init checkpoints/bc.pt --states 2000000
    python train.py eval --ckpt checkpoints/best.pt
    python train.py eval --ckpt checkpoints/best.pt --vs-ckpt checkpoints/other.pt
    python train.py elo --ckpts a.pt b.pt --scripted heuristic maxpower random

Needs one local Showdown server (see README), or --showdown-cmd to start it:
    node pokemon-showdown start --no-security

`--size small` keeps the MiniPokeV1 recipe (mirror self-play, constant LR and
entropy). `--size wide` is MiniPokeV1_4M's architecture, trained with the same
recipe. `--size medium` turns on the long-run defaults: LR warmup + cosine,
entropy annealing, the opponent pool and a distributional value head.
`--size wide-medium` is wide's architecture with medium's value head and defaults.
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import random
import time
from pathlib import Path
from typing import Dict, Optional

os.environ.setdefault("PYTORCH_ENABLE_MPS_FALLBACK", "1")  # must precede torch import

import torch

import runtime
from checkpoint import (load_checkpoint, rng_state, save_checkpoint, seed_everything,
                        set_rng_state)
from dataset import ShardDataset, ShardWriter
from elo import LEARNER, EloTable, fit_ratings, parse_anchors, round_robin_pairs, snapshot_name
from encoder_v2 import model_encoder
from export_onnx import export_checkpoint, model_inputs, random_inputs
from league import OpponentPool, split_battles
from model import (PRESETS, MiniPokeTransformer, ModelConfig, build_model, describe, pick_device,
                   preset, size_of, with_aux_heads)
from players import DistillPlayer, InferenceBatcher, Rewards, RLPlayer, TeacherPlayer, faint_reward
from ppo import AUX_STATS, Budget, PPOConfig, Schedule, distill_train, ppo_update
from rollout import (SCRIPTED, RolloutConfig, Rollouts, close_players, evaluate, frozen_copy,
                     load_into, make_kwargs, play_match, play_recording_pairs, wilson_interval)
from setpriors import SetPriors
from runtime import (MetricsLogger, ShowdownServer, StopController, peak_rss_mb,
                     pick_infer_device, prune_checkpoints, rss_mb)

log = logging.getLogger("minipoke")

# Per-size defaults for every option left unset on the command line. "small"
# is exactly the MiniPokeV1 recipe; don't change it, or V1 stops reproducing.
DEFAULTS: Dict[str, Dict[str, object]] = {
    "small": dict(
        concurrency=32, battles_per_iter=128, lr=2e-4, warmup_iters=0, lr_min_ratio=1.0,
        ent_start=0.01, ent_end=0.01, mirror_share=1.0, pool_share=0.0, heuristic_share=0.0,
        bc_value_coef=0.0, value_ce_norm="log-bins", vf_coef=0.5, kl_stop="last",
        pool_sampling="uniform",
    ),
    "medium": dict(
        # lr 1e-4: at 2e-4 (with the raw two-hot loss at 0.5) PPO stalled around 43% vs the
        # heuristic. vf_coef 0.5 on the normalized two-hot loss weighs about as much as the
        # 0.1 on the raw loss that took that run to 55% (see --value-ce-norm).
        concurrency=48, battles_per_iter=192, lr=1e-4, warmup_iters=10, lr_min_ratio=0.1,
        ent_start=0.01, ent_end=0.003, mirror_share=0.7, pool_share=0.2, heuristic_share=0.1,
        bc_value_coef=0.5, value_ce_norm="log-bins", vf_coef=0.5, kl_stop="mean",
        pool_sampling="pfsp",
    ),
}
DEFAULTS["wide"] = DEFAULTS["small"]  # MiniPokeV1_4M was trained with the V1 recipe
DEFAULTS["wide-medium"] = DEFAULTS["medium"]
# --value-ce-norm none is the two-hot loss before it was divided by log(bins); medium's
# value weight was 0.1 then, so that setting also brings back the old medium default.
RAW_CE_VF_COEF = {"medium": 0.1, "wide-medium": 0.1}
# Options added after a run was started: resuming it keeps what it was trained with
LEGACY = dict(value_ce_norm="none", kl_stop="last", pool_sampling="uniform")
COMMON_DEFAULTS = dict(
    iterations=None, hours=None, states=None, confirm_battles=400, minibatch=1024, eval_every=10, eval_battles=200,
    eval_pool_battles=100, min_eval_battles=150, snapshot_every=10, pool_cap=20, pool_slots=2,
    save_every=10, stall_timeout=180.0, infer_device="auto", seed=None,
    max_infer_batch=64, faint_anneal_states=None, pfsp_floor=0.1, pool_ema=0.05,
    elo_anchors=None, elo_k=16.0, reward="shaped", uniform_kl_coef=0.0,
    aux_action_coef=0.0, aux_set_coef=0.0,
)


def resolve(args: argparse.Namespace, size: str, keys) -> None:
    """Fill options left at None from the size's defaults."""
    for k in keys:
        if getattr(args, k, None) is None:
            if k == "vf_coef" and getattr(args, "value_ce_norm", None) == "none":
                setattr(args, k, RAW_CE_VF_COEF.get(size, DEFAULTS[size][k]))
                continue
            setattr(args, k, DEFAULTS[size].get(k, COMMON_DEFAULTS.get(k)))


def setup(args, run_dir: Optional[Path] = None) -> torch.device:
    runtime.setup_logging(run_dir / "train.log" if run_dir else None)
    runtime.install_mps_fallback_warning()
    torch.set_num_threads(max(1, (os.cpu_count() or 4) // 2))
    return pick_device(args.device)


def sample_batch(n: int, model) -> Dict[str, torch.Tensor]:
    return dict(zip(model_inputs(model), random_inputs(n, model=model)))


def choose_infer_device(args, model, train_device) -> torch.device:
    if args.infer_device != "auto":
        return torch.device(args.infer_device)
    return pick_infer_device(model.to("cpu"), lambda n: sample_batch(n, model), train_device,
                             args.concurrency)


def server_for(args, run_dir: Optional[Path]) -> ShowdownServer:
    server = ShowdownServer(args.showdown_cmd, args.showdown_cwd, port=args.showdown_port,
                            log_path=(run_dir / "showdown.log") if run_dir else None)
    if not server.ensure_up():
        raise SystemExit("no Showdown server")
    return server


def new_model(args) -> MiniPokeTransformer:
    cfg = preset(args.size, dropout=args.dropout, value_bins=args.value_bins,
                 encoder_version=args.encoder_version, cand_moves=args.cand_moves,
                 move_tokens=args.move_tokens or None, history=args.history or None,
                 typed_ffn=args.typed_ffn or None, history_tokens=args.history_tokens,
                 matchup_tokens=args.matchup_tokens or None)
    model = MiniPokeTransformer(cfg)
    if cfg.encoder_version >= 2:
        model.priors = SetPriors.from_showdown(args.sets, args.showdown_cwd)
        log.info("set priors: %d species from %s (sha256 %s)", len(model.priors.species),
                 model.priors.source, model.priors.source_sha256[:12])
    return model


def eval_vs(model, infer_device, n: int, concurrency: int, opponent: str = "heuristic"):
    opp_cls = SCRIPTED[opponent]
    batcher = InferenceBatcher(frozen_copy(model, infer_device), infer_device,
                               max_batch=concurrency * 2)
    return evaluate(batcher, lambda: opp_cls(**make_kwargs(concurrency)), n, concurrency)


def head_to_head(model, other, infer_device, n: int, concurrency: int):
    """Greedy `model` vs greedy `other`. -> (model's win rate, finished battles)."""
    batcher, other_batcher = (InferenceBatcher(frozen_copy(m, infer_device), infer_device,
                                               max_batch=concurrency * 2) for m in (model, other))
    return evaluate(batcher, lambda: RLPlayer(other_batcher, None, greedy=True,
                                              **make_kwargs(concurrency)), n, concurrency)


def with_ci(wr: float, n: int) -> str:
    lo, hi = wilson_interval(wr * n, n)
    return f"{wr:.1%} over {n} battles (95% CI [{lo:.1%}, {hi:.1%}])"


def eval_heuristic(model, infer_device, n: int, concurrency: int) -> float:
    wr, n_done = eval_vs(model, infer_device, n, concurrency)
    log.info("win rate vs SimpleHeuristicsPlayer: %s", with_ci(wr, n_done))
    return wr


# --------------------------------------------------------------------------
# Behavioural cloning / distillation
# --------------------------------------------------------------------------
def collect(args, writer: ShardWriter, make_pair, server) -> None:
    t0 = time.time()
    stop = StopController().install()
    n = play_recording_pairs(make_pair, args.battles, chunk=args.chunk,
                             stall_timeout=args.stall_timeout, server=server, stop=stop)
    writer.close()
    log.info("[data] %d battles, %d samples in %.0fs", n, writer.n_samples, time.time() - t0)


def train_on_dataset(args, model, device, data: ShardDataset, vf_coef: float,
                     value_target: str, out: Path, tag: str) -> None:
    if data.obs_layout != model_encoder(model).layout:
        raise SystemExit(f"{data.root} holds observations of another encoder than the model's "
                         f"(encoder_version {model.cfg.encoder_version}): use another --data dir")
    log.info("[%s] dataset %s: %d samples (%d train / %d val), %.1f MB on disk "
             "(%.0f bytes/sample), process RSS %.0f MB",
             tag, data.root, len(data), data.n_samples("train"), data.n_samples("val"),
             data.bytes_on_disk() / 2**20, data.bytes_on_disk() / max(len(data), 1), rss_mb() or -1)
    model = model.to(device)
    log.info("[%s] model: %s on %s", tag, describe(model), device)
    runtime.mps_self_check(model, device, sample_batch(8, model))
    opt = torch.optim.AdamW(model.parameters(), lr=args.lr, weight_decay=1e-2)
    distill_train(model, opt, data, args.epochs, args.minibatch, device, vf_coef=vf_coef,
                  value_target=value_target, seed=args.seed or 0, log_prefix=f"[{tag}]",
                  value_ce_norm=args.value_ce_norm == "log-bins")
    save_checkpoint(out, model)
    log.info("[%s] saved %s; peak RSS %.0f MB", tag, out, peak_rss_mb() or -1)


def data_dir(args, tag: str) -> Path:
    return Path(args.data or f"data/{tag}-{time.strftime('%Y%m%d-%H%M%S')}")


def cmd_imitate(args) -> None:
    resolve(args, args.size, ["concurrency", "bc_value_coef", "infer_device", "seed"])
    device = setup(args)
    if args.seed is not None:
        seed_everything(args.seed)
    root = data_dir(args, "bc")
    server = server_for(args, None)
    model = new_model(args)
    encoder = model_encoder(model)
    if not (root / "meta.json").exists():
        writer = ShardWriter(root, args.shard_size, info={"teacher": "SimpleHeuristicsPlayer",
                                                          "epsilon": args.epsilon},
                             obs_layout=encoder.layout)
        gamma = PPOConfig().gamma
        log.info("[bc] playing %d heuristic-vs-heuristic battles into %s...", args.battles, root)
        collect(args, writer, lambda: tuple(
            TeacherPlayer(writer, gamma, args.epsilon, encoder=encoder,
                          **make_kwargs(args.concurrency))
            for _ in range(2)), server)
    else:
        log.info("[bc] reusing the dataset in %s", root)
    train_on_dataset(args, model, device, ShardDataset(root), args.bc_value_coef, "return",
                     CKPT_DIR(args) / "bc.pt", "bc")
    infer = torch.device(args.infer_device if args.infer_device != "auto" else "cpu")
    eval_heuristic(model, infer, args.eval_battles, args.concurrency)
    server.stop()


def cmd_distill(args) -> None:
    resolve(args, args.size, ["concurrency", "infer_device", "seed"])
    device = setup(args)
    if args.seed is not None:
        seed_everything(args.seed)
    infer = torch.device(args.infer_device if args.infer_device != "auto" else "cpu")
    teacher = build_model(load_checkpoint(args.teacher))
    log.info("[distill] teacher %s: %s", args.teacher, describe(teacher))
    root = data_dir(args, "distill")
    server = server_for(args, None)
    model = new_model(args)
    student = model_encoder(model)
    if not (root / "meta.json").exists():
        batcher = InferenceBatcher(frozen_copy(teacher, infer), infer,
                                   max_batch=args.concurrency * 2)
        # The teacher plays from its own encoding; the samples hold the student's
        writer = ShardWriter(root, args.shard_size, info={
            "teacher": str(args.teacher), "epsilon": args.epsilon,
            "teacher_encoder_version": batcher.encoder.version,
            "student_encoder_version": student.version}, obs_layout=student.layout)
        gamma = PPOConfig().gamma
        log.info("[distill] playing %d teacher-vs-teacher battles into %s (teacher encoder v%d, "
                 "student v%d)...", args.battles, root, batcher.encoder.version, student.version)
        collect(args, writer, lambda: tuple(
            DistillPlayer(batcher, writer, gamma, args.epsilon, encoder=student,
                          **make_kwargs(args.concurrency))
            for _ in range(2)), server)
    else:
        log.info("[distill] reusing the dataset in %s", root)
    train_on_dataset(args, model, device, ShardDataset(root), args.vf_coef, args.value_target,
                     CKPT_DIR(args) / "distill.pt", "distill")
    eval_heuristic(model, infer, args.eval_battles, args.concurrency)
    server.stop()


# --------------------------------------------------------------------------
# PPO
# --------------------------------------------------------------------------
PPO_KEYS = list(DEFAULTS["small"]) + list(COMMON_DEFAULTS)
METRICS = ["iteration", "time", "hours", "states", "total_states", "battles", "play_s", "learn_s",
           "states_per_s", "lr", "ent_coef", "pi_loss", "v_loss", "v_mse", "entropy", "kl",
           "clipfrac", "explained_var", "uniform_kl", "aux_labelled", "aux_action_loss",
           "aux_action_acc", "aux_set_loss", "aux_item_acc", "aux_ability_acc", "aux_tera_acc",
           "aux_moves_recall", "train_wr_pool", "train_wr_heuristic",
           "eval_wr_heuristic", "eval_n_heuristic", "eval_wr_pool", "eval_n_pool", "confirm_wr",
           "confirm_n", "best_wr", "incumbent_wr", "incumbent_n", "elo", "epochs", "faint_reward", "pool_size",
           "pool_hardest_wr", "stalled", "rebuilds", "rss_mb"]


RUN_KEYS = ["run", "size", "ckpt_dir", "export_dir"]


def CKPT_DIR(args) -> Path:
    return Path(args.ckpt_dir or "checkpoints")


def cmd_ppo(args) -> None:
    resumed = load_checkpoint(args.resume) if args.resume else None
    if resumed:
        # Saved options, overridden by anything given on this command line
        saved = {**LEGACY, **(resumed.get("args") or {})}
        for k, v in saved.items():
            if getattr(args, k, None) is None and k in PPO_KEYS + RUN_KEYS:
                setattr(args, k, v)
        # Keep writing where the run lives (checkpoints saved before these were stored)
        args.ckpt_dir = args.ckpt_dir or str(Path(args.resume).parent)
    args.export_dir = args.export_dir or "export"
    init = load_checkpoint(args.init) if args.init and not resumed else None
    start_from = resumed or init
    if args.size is None and start_from is not None:
        # The defaults follow the model being trained, not the "small" fallback
        args.size = size_of(ModelConfig.from_dict(start_from["cfg"]))
    size = args.size or "small"
    args.size = size
    resolve(args, size, PPO_KEYS)
    if args.iterations is None and args.hours is None and args.states is None:
        args.iterations = 300  # the V1 recipe
    args.run = args.run or f"{size}-{time.strftime('%Y%m%d-%H%M%S')}"
    run_dir = Path("runs") / args.run
    ckpt_dir = CKPT_DIR(args)
    device = setup(args, run_dir)
    if not resumed and (ckpt_dir / "ppo_latest.pt").exists():
        raise SystemExit(f"{ckpt_dir} already holds a run (ppo_latest.pt): continue it with "
                         f"--resume {ckpt_dir / 'ppo_latest.pt'}, or start a new one with another "
                         f"--ckpt-dir")
    stop = StopController(run_dir / "STOP").install()
    (run_dir / "STOP").unlink(missing_ok=True)
    server = server_for(args, run_dir)

    # ---- model, optimizer, state
    if resumed:
        model = build_model(resumed)
    elif init is not None:
        model = build_model(init)
    else:
        model = new_model(args)
    if args.seed is not None and not resumed:
        seed_everything(args.seed)
    use_aux = bool(args.aux_action_coef or args.aux_set_coef)
    if use_aux and not model.cfg.aux_heads:
        if resumed:
            raise SystemExit("this run has no auxiliary heads: start a new run (--init) to "
                             "use --aux-action-coef / --aux-set-coef")
        model = with_aux_heads(model)
    model = model.to(device)
    cfg = PPOConfig(lr=args.lr, minibatch=args.minibatch, vf_coef=args.vf_coef,
                    kl_stop=args.kl_stop, value_ce_norm=args.value_ce_norm == "log-bins",
                    uniform_kl_coef=args.uniform_kl_coef, aux_action_coef=args.aux_action_coef,
                    aux_set_coef=args.aux_set_coef)
    if args.reward == "winloss":
        cfg.gamma = 1.0  # undiscounted: the value is the win probability
    sched = Schedule(lr=args.lr, warmup_iters=args.warmup_iters, lr_min_ratio=args.lr_min_ratio,
                     ent_start=args.ent_start, ent_end=args.ent_end)
    opt = torch.optim.AdamW(model.parameters(), lr=cfg.lr, weight_decay=1e-2)
    pool = OpponentPool(ckpt_dir / "pool", cap=args.pool_cap, sampling=args.pool_sampling,
                        floor=args.pfsp_floor, ema=args.pool_ema)
    # best_wr: best.pt's win rate, first the one that made it best.pt, then the one
    # it keeps on fresh battles (best_wins / best_n) when candidates challenge it
    st = {"iteration": 0, "total_states": 0, "active_seconds": 0.0, "best_wr": -1.0,
          "best_iteration": None, "best_wins": 0.0, "best_n": 0}
    best_path = ckpt_dir / "best.pt"
    best_state = None   # best.pt's weights
    if resumed:
        opt.load_state_dict(resumed["opt"])
        # Checkpoints saved before the full training state only hold the iteration
        st.update(resumed.get("state") or {"iteration": resumed.get("iteration", 0)})
        if best_path.exists():
            best_state = load_checkpoint(best_path)["model"]
        pool.load_state_dict(resumed.get("pool"))
        set_rng_state(resumed.get("rng"))
        log.info("[ppo] resumed %s at iteration %d (%s states, %.2f h)", args.resume,
                 st["iteration"], f"{st['total_states']:,}", st["active_seconds"] / 3600)
    use_pool = args.pool_share > 0
    pool_rng = random.Random((args.seed or 0) + st["iteration"])
    elo = EloTable(parse_anchors(args.elo_anchors), k=args.elo_k)
    elo.add_player(LEARNER, elo.rating("heuristic"))
    if resumed:
        elo.load_state_dict(resumed.get("elo"))

    log.info("[ppo] run %s, size %s: %s, train on %s", args.run, size, describe(model), device)
    log.info("[ppo] budget: %s iterations, %s hours, %s states; battles/iter %d (mirror %.0f%% / "
             "pool %.0f%% %s / heuristic %.0f%%), concurrency %d", args.iterations, args.hours,
             args.states, args.battles_per_iter, 100 * args.mirror_share, 100 * args.pool_share,
             args.pool_sampling, 100 * args.heuristic_share, args.concurrency)
    runtime.mps_self_check(model, device, sample_batch(8, model))
    infer_device = choose_infer_device(args, model, device)
    model.to(device)
    infer_model = frozen_copy(model, infer_device)
    rewards = Rewards.named(args.reward)
    rollouts = Rollouts(infer_model, infer_device, RolloutConfig(
        concurrency=args.concurrency, mirror_share=args.mirror_share, pool_share=args.pool_share,
        heuristic_share=args.heuristic_share, pool_slots=args.pool_slots,
        max_infer_batch=args.max_infer_batch, stall_timeout=args.stall_timeout,
        aux_labels=use_aux), server, rewards=rewards)
    metrics = MetricsLogger(run_dir / "metrics.csv", METRICS)
    if use_pool and len(pool) == 0:
        pool.add(model, st["iteration"])  # the starting policy (BC / distill) is the first opponent
        elo.add_player(snapshot_name(st["iteration"]))

    def sync() -> None:
        load_into(infer_model, model.state_dict())

    def challenge(it: int, score: float, n_score: int, row: dict, **info) -> None:
        """`model` (iteration `it`) won `score` of `n_score` fresh battles: it becomes
        best.pt if that beats best.pt's win rate, measured again on fresh battles.
        The score that made best.pt win is optimistic (the luckiest of many), so
        without that a lucky early best.pt would outlive better policies."""
        nonlocal best_state
        if args.confirm_battles and best_state is not None:
            wr, n = rollouts.evaluate_weights(best_state, args.confirm_battles)
            st["best_wins"] += wr * n
            st["best_n"] += n
            if st["best_n"]:
                st["best_wr"] = st["best_wins"] / st["best_n"]
                row.update(incumbent_wr=st["best_wr"], incumbent_n=st["best_n"])
                log.info("[ppo] best.pt (it %s) re-evaluated: %s", st["best_iteration"],
                         with_ci(st["best_wr"], st["best_n"]))
        if (n_score >= min(args.min_eval_battles, args.confirm_battles or n_score)
                and score > st["best_wr"]):
            st["best_wr"], st["best_iteration"] = score, it
            st["best_wins"], st["best_n"] = 0.0, 0   # the battles that selected it do not count
            best_state = {k: v.detach().cpu().clone() for k, v in model.state_dict().items()}
            save_checkpoint(best_path, model, None, iteration=it, **info)
            log.info("[ppo] new best: %.1f%% at iteration %d -> %s", 100 * score, it, best_path)

    budget = Budget(args.iterations, args.hours, args.states)

    def progress() -> float:
        return budget.progress(st["iteration"], st["active_seconds"], st["total_states"])

    def save_all(periodic: bool) -> None:
        extra = dict(args={k: getattr(args, k) for k in PPO_KEYS + RUN_KEYS},
                     state=dict(st), pool=pool.state_dict(), rng=rng_state(),
                     iteration=st["iteration"], elo=elo.state_dict())
        save_checkpoint(ckpt_dir / "ppo_latest.pt", model, opt, **extra)
        if periodic:
            save_checkpoint(ckpt_dir / f"ppo_{st['iteration']:05d}.pt", model, opt, **extra)
            prune_checkpoints(ckpt_dir, keep_last=5, keep_every=50)

    consecutive_failures = 0
    while not stop.stop and progress() < 1.0:
        t_iter = time.time()
        st["iteration"] += 1
        it = st["iteration"]
        lr, ent = sched.lr_at(it, progress()), sched.ent_at(progress())
        for g in opt.param_groups:
            g["lr"] = lr

        # ---- play
        sync()
        if args.reward == "shaped":
            rewards.faint = faint_reward(st["total_states"], args.faint_anneal_states)
        n_mirror, n_pool, n_heur = split_battles(args.battles_per_iter, args.mirror_share,
                                                 args.pool_share if use_pool else 0.0,
                                                 args.heuristic_share)
        sampled = pool.sample(args.pool_slots, pool_rng) if use_pool else []
        if sampled:
            rollouts.load_slots([pool.load_weights(s) for s in sampled])
        res = rollouts.play_iteration(n_mirror, n_pool, n_heur)
        for i, (wins, n_done) in enumerate(res.slot_results if sampled else []):
            snap = sampled[i % len(sampled)]
            pool.record(snap.iteration, wins, n_done)
            elo.record(LEARNER, snapshot_name(snap.iteration), wins, n_done)
        if res.heuristic_result:
            elo.record(LEARNER, "heuristic", *res.heuristic_result)
        t_play = time.time() - t_iter
        episodes = res.episodes
        consecutive_failures = 0 if res.play.ok else consecutive_failures + 1
        if consecutive_failures >= 5:
            log.error("[ppo] 5 failed iterations in a row: stopping")
            stop.request("too many failures")
        if not episodes:
            st["iteration"] -= 1
            st["active_seconds"] += time.time() - t_iter
            continue

        # ---- learn
        t0 = time.time()
        stats = ppo_update(model, opt, episodes, cfg, device, ent_coef=ent)
        t_learn = time.time() - t0
        if stats["skipped"]:
            log.warning("[ppo] it %d: skipped %d minibatch(es) with a non-finite loss or gradient",
                        it, stats["skipped"])
        st["total_states"] += stats["samples"]
        row = dict(iteration=it, time=time.strftime("%Y-%m-%d %H:%M:%S"), states=stats["samples"],
                   total_states=st["total_states"], battles=len(episodes), play_s=t_play,
                   learn_s=t_learn, states_per_s=stats["samples"] / (t_play + t_learn), lr=lr,
                   ent_coef=ent, train_wr_pool=res.win_rates.get("pool"),
                   train_wr_heuristic=res.win_rates.get("heuristic"), pool_size=len(pool),
                   pool_hardest_wr=min((s.win_rate for s in pool.snapshots), default=None),
                   faint_reward=rewards.faint, epochs=stats["epochs"],
                   aux_labelled=res.aux_labelled, **{k: stats[k] for k in AUX_STATS},
                   stalled=int(not res.play.ok), rebuilds=rollouts.rebuilds,
                   **{k: stats[k] for k in ("pi_loss", "v_loss", "v_mse", "entropy", "kl",
                                            "clipfrac", "explained_var", "uniform_kl")})
        log.info("[ppo] it %4d  states %6d (total %s)  play %5.1fs  learn %4.1fs  %5.0f st/s  "
                 "lr %.2e  ent_c %.4f  pi %+.3f  v %.3f  ent %.2f  kl %.4f  clip %.3f  ev %.2f",
                 it, stats["samples"], f"{st['total_states']:,}", t_play, t_learn,
                 row["states_per_s"], lr, ent, stats["pi_loss"], stats["v_loss"],
                 stats["entropy"], stats["kl"], stats["clipfrac"], stats["explained_var"])

        if use_pool and it % args.snapshot_every == 0:
            pool.add(model, it)
            elo.add_player(snapshot_name(it))

        # ---- evaluate
        if it % args.eval_every == 0:
            sync()
            wr, n_done = rollouts.evaluate(args.eval_battles, "heuristic")
            row.update(eval_wr_heuristic=wr, eval_n_heuristic=n_done)
            elo.record(LEARNER, "heuristic", wr * n_done, n_done)
            msg = f"[ppo] it {it}: greedy win rate vs SimpleHeuristicsPlayer = {with_ci(wr, n_done)}"
            if use_pool:
                wins = total = 0
                for snap in [s for s in pool.recent(4) if s.iteration != it][:3]:
                    pwr, pn = rollouts.evaluate(args.eval_pool_battles, "pool", pool.load_weights(snap))
                    elo.record(LEARNER, snapshot_name(snap.iteration), pwr * pn, pn)
                    wins += pwr * pn
                    total += pn
                    msg += f", vs it {snap.iteration} = {pwr:.1%}"
                if total:
                    row.update(eval_wr_pool=wins / total, eval_n_pool=total)
            log.info(msg)
            if n_done >= args.min_eval_battles and wr > st["best_wr"]:
                # The best of many noisy evaluations is optimistic (winner's curse): a
                # candidate only becomes best.pt on its win rate over fresh battles.
                score, n_score = wr, n_done
                if args.confirm_battles:
                    score, n_score = rollouts.evaluate(args.confirm_battles, "heuristic")
                    row.update(confirm_wr=score, confirm_n=n_score)
                    elo.record(LEARNER, "heuristic", score * n_score, n_score)
                    log.info("[ppo] it %d: candidate (%.1f%%) confirmed at %s", it, 100 * wr,
                             with_ci(score, n_score))
                challenge(it, score, n_score, row, eval_wr_heuristic=wr,
                          confirm_wr=row.get("confirm_wr"))

        st["active_seconds"] += time.time() - t_iter
        row.update(hours=st["active_seconds"] / 3600, best_wr=max(st["best_wr"], 0.0),
                   rss_mb=rss_mb(), elo=elo.rating(LEARNER))
        metrics.append(row)
        elo.save(run_dir / "elo.json")
        save_all(periodic=it % args.save_every == 0)

    # ---- end of run: the last policy challenges best.pt too (candidates otherwise only
    # come from evaluations every --eval-every iterations that beat best.pt's win rate)
    it = st["iteration"]
    if it and it not in (st["best_iteration"], st.get("final_iteration")):
        sync()
        score, n_score = rollouts.evaluate(args.confirm_battles or args.eval_battles, "heuristic")
        elo.record(LEARNER, "heuristic", score * n_score, n_score)
        elo.save(run_dir / "elo.json")
        st["final_iteration"], st["final_wr"], st["final_n"] = it, score, n_score
        log.info("[ppo] final policy (it %d): greedy win rate vs SimpleHeuristicsPlayer = %s",
                 it, with_ci(score, n_score))
        challenge(it, score, n_score, {}, final_wr=score)
    save_all(periodic=True)
    log.info("[ppo] stopped at iteration %d (%s): %s states in %.2f h, peak RSS %.0f MB",
             st["iteration"], stop.reason or "budget reached", f"{st['total_states']:,}",
             st["active_seconds"] / 3600, peak_rss_mb() or -1)
    close_players(rollouts.players)
    export_checkpoint(best_path if best_path.exists() else ckpt_dir / "ppo_latest.pt",
                      args.export_dir)
    server.stop()


def cmd_eval(args) -> None:
    setup(args)
    infer = torch.device(args.infer_device or "cpu")
    model = build_model(load_checkpoint(args.ckpt))
    log.info("%s: %s", args.ckpt, describe(model))
    server = server_for(args, None)
    if args.vs_ckpt:
        other = build_model(load_checkpoint(args.vs_ckpt))
        log.info("%s: %s", args.vs_ckpt, describe(other))
        wr, n = head_to_head(model, other, infer, args.battles, args.concurrency)
        label = f"{args.vs_ckpt} (greedy)"
    else:
        wr, n = eval_vs(model, infer, args.battles, args.concurrency, args.opponent)
        label = args.opponent
    server.stop()
    print(f"win rate vs {label}: {with_ci(wr, n)}")


def cmd_elo(args) -> None:
    """Round-robin between checkpoints and scripted players, rated by maximum
    likelihood with the scripted players as anchors."""
    setup(args)
    infer = torch.device(args.infer_device or "cpu")
    makers = {}
    for path in args.ckpts:
        model = build_model(load_checkpoint(path))
        name = Path(path).stem if sum(Path(p).stem == Path(path).stem for p in args.ckpts) == 1 \
            else str(path)
        log.info("%s: %s", name, describe(model))
        batcher = InferenceBatcher(frozen_copy(model, infer), infer, max_batch=args.concurrency * 2)
        makers[name] = (lambda b: lambda: RLPlayer(b, None, greedy=True,
                                                   **make_kwargs(args.concurrency)))(batcher)
    for name in args.scripted:
        makers[name] = (lambda cls: lambda: cls(**make_kwargs(args.concurrency)))(SCRIPTED[name])
    anchors = {k: v for k, v in parse_anchors(args.anchors).items() if k in makers}
    server = server_for(args, None)
    results = {}
    for a, b in round_robin_pairs(makers):
        score, n = play_match(makers[a], makers[b], args.battles, args.stall_timeout, server)
        results[(a, b)] = (score, n)
        print(f"{a} vs {b}: {with_ci(score / max(n, 1), n)}", flush=True)
    server.stop()
    ratings = fit_ratings(results, anchors)
    print(f"\nratings (anchors {anchors or 'none: centred on 0'}):")
    for name, r in sorted(ratings.items(), key=lambda kv: -kv[1]):
        print(f"  {name:30s} {r:7.1f}{'  (anchor)' if name in anchors else ''}")
    if args.out:
        Path(args.out).write_text(json.dumps({
            "anchors": anchors, "ratings": ratings,
            "results": [{"a": a, "b": b, "score_a": s, "games": n} for (a, b), (s, n)
                        in results.items()]}, indent=1))
        print(f"wrote {args.out}")


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)

    def common(p):
        p.add_argument("--device", default="auto", help="training device: auto|mps|cuda|cpu")
        p.add_argument("--infer-device", default=None,
                       help="rollout inference device; auto (default for ppo) benchmarks CPU "
                            "against the training device and keeps the faster")
        p.add_argument("--concurrency", type=int, default=None,
                       help="concurrent battles per seat pair (default 32 small/wide, 48 medium/wide-medium)")
        p.add_argument("--eval-battles", type=int, default=200)
        p.add_argument("--ckpt-dir", default=None,
                       help="default checkpoints/ (ppo --resume: the resumed run's directory)")
        p.add_argument("--seed", type=int, default=None)
        p.add_argument("--stall-timeout", type=float, default=180.0,
                       help="cancel and rebuild players if no battle finishes for this long (s)")
        p.add_argument("--showdown-cmd", default=None,
                       help='(re)start the server when it is down, e.g. '
                            '"node pokemon-showdown start --no-security"')
        p.add_argument("--showdown-cwd", default="pokemon-showdown")
        p.add_argument("--showdown-port", type=int, default=8000)

    def model_args(p, default_size):
        p.add_argument("--size", default=default_size, choices=list(PRESETS))
        p.add_argument("--dropout", type=float, default=None)
        p.add_argument("--value-bins", type=int, default=None,
                       help="1 = scalar value head; default 1 small/wide, 51 medium/wide-medium")
        arch_args(p)

    def arch_args(p):
        """Architecture of a new model (--init / --resume keep the checkpoint's)."""
        p.add_argument("--encoder-version", type=int, default=None, choices=[1, 2],
                       help="observation layout of a new model: 1 = encoder.py (default), "
                            "2 = encoder_v2.py (minipoke-v2: random battle set priors)")
        p.add_argument("--cand-moves", type=int, default=None,
                       help="v2: candidate move slots per opponent Pokémon (default 8)")
        p.add_argument("--sets", default=None,
                       help="v2: Showdown's gen 9 random battle sets file or Showdown clone "
                            "(default: the one in --showdown-cwd)")
        p.add_argument("--history", action="store_true",
                       help="v2: last moves, switches, protects and a choice-lock flag "
                            "(encoder_v2 hist_ids / hist_feats) in the field token")
        p.add_argument("--history-tokens", type=int, default=None,
                       help="v2: the last K battle events (moves, switches and their results) "
                            "as tokens; replaces --history (default 0)")
        p.add_argument("--matchup-tokens", action="store_true",
                       help="v2: damage-calc matchup tokens (each of ours vs their active, our "
                            "active vs each of theirs), injected after layer n_layers // 2")
        p.add_argument("--typed-ffn", action="store_true",
                       help="one feed-forward block per token type in every layer")
        p.add_argument("--move-tokens", action="store_true",
                       help="one token per (Pokémon, move slot), candidates included; the move "
                            "head reads our active's move tokens after the transformer")

    def value_ce_arg(p, default="log-bins"):
        p.add_argument("--value-ce-norm", default=default, choices=["log-bins", "none"],
                       help="divide the two-hot value cross-entropy by log(bins) (~1 at init, "
                            "like the scalar head's MSE); none = the raw cross-entropy")

    def data_args(p, battles, epsilon):
        p.add_argument("--battles", type=int, default=battles)
        p.add_argument("--epochs", type=int, default=8)
        p.add_argument("--minibatch", type=int, default=512)
        p.add_argument("--lr", type=float, default=5e-4)
        p.add_argument("--epsilon", type=float, default=epsilon)
        p.add_argument("--data", default=None,
                       help="dataset directory; reused without playing if it already holds one")
        p.add_argument("--shard-size", type=int, default=16384)
        p.add_argument("--chunk", type=int, default=500, help="battles per watchdog chunk")

    p = sub.add_parser("imitate", help="behavioural cloning of SimpleHeuristicsPlayer")
    common(p)
    model_args(p, "small")
    data_args(p, 3000, 0.1)
    p.add_argument("--bc-value-coef", type=float, default=None,
                   help="also fit the value head to battle returns (default 0 small/wide, 0.5 medium/wide-medium)")
    value_ce_arg(p)
    p.set_defaults(fn=cmd_imitate)

    p = sub.add_parser("distill", help="clone a trained model's full action distribution (the "
                                        "student may use another encoder, e.g. V1 -> v2)")
    common(p)
    model_args(p, "medium")
    data_args(p, 3000, 0.05)
    p.add_argument("--teacher", required=True)
    p.add_argument("--vf-coef", type=float, default=0.5)
    p.add_argument("--value-target", default="teacher", choices=["teacher", "return"])
    value_ce_arg(p)
    p.set_defaults(fn=cmd_distill)

    p = sub.add_parser("ppo")
    common(p)
    p.set_defaults(eval_battles=None, stall_timeout=None)
    p.add_argument("--size", default=None, choices=list(PRESETS),
                   help="for a new model and its defaults; --init/--resume keep their architecture")
    p.add_argument("--dropout", type=float, default=None)
    p.add_argument("--value-bins", type=int, default=None)
    arch_args(p)
    p.add_argument("--init", default=None, help="checkpoint to start from (e.g. checkpoints/bc.pt)")
    p.add_argument("--resume", default=None, help="continue a run (e.g. checkpoints/ppo_latest.pt)")
    p.add_argument("--run", default=None, help="name under runs/ (metrics.csv, train.log)")
    p.add_argument("--iterations", type=int, default=None,
                   help="total iteration budget (default 300 when --hours is not set)")
    p.add_argument("--hours", type=float, default=None,
                   help="total training-time budget; resuming counts the time already spent")
    p.add_argument("--states", type=int, default=None,
                   help="total training-states budget (compare runs at matched states); the "
                        "run stops at whichever of --iterations / --hours / --states ends first")
    p.add_argument("--battles-per-iter", type=int, default=None)
    p.add_argument("--minibatch", type=int, default=None)
    p.add_argument("--lr", type=float, default=None, help="peak learning rate")
    p.add_argument("--vf-coef", type=float, default=None,
                   help="value loss weight (default 0.5; 0.1 for medium with --value-ce-norm none)")
    value_ce_arg(p, default=None)
    p.add_argument("--kl-stop", default=None, choices=["mean", "last"],
                   help="stop the PPO epochs when the epoch's mean approx KL (default medium) "
                        "or its last minibatch's (V1, default small/wide) exceeds 0.03")
    p.add_argument("--reward", default=None, choices=["shaped", "winloss"],
                   help="shaped (default, V1): +-1 at the end, +-0.1 per faint, gamma 0.995; "
                        "winloss: 1 for a win, 0 otherwise, no shaping, gamma 1")
    p.add_argument("--uniform-kl-coef", type=float, default=None,
                   help="weight of KL(uniform over the legal actions || policy) added to the "
                        "loss (default 0)")
    p.add_argument("--aux-action-coef", type=float, default=None,
                   help="weight of the auxiliary loss predicting the opponent's action, "
                        "labelled from the other seat of mirror / pool games (default 0)")
    p.add_argument("--aux-set-coef", type=float, default=None,
                   help="weight of the auxiliary losses predicting each opponent Pokémon's "
                        "item, ability, tera type and moves (default 0)")
    p.add_argument("--faint-anneal-states", type=int, default=None,
                   help="take the faint reward linearly from 0.1 to 0 over this many training "
                        "states (default off)")
    p.add_argument("--warmup-iters", type=int, default=None)
    p.add_argument("--lr-min-ratio", type=float, default=None,
                   help="final LR / peak LR of the cosine decay (1 = constant)")
    p.add_argument("--ent-start", type=float, default=None)
    p.add_argument("--ent-end", type=float, default=None)
    p.add_argument("--mirror-share", type=float, default=None)
    p.add_argument("--pool-share", type=float, default=None)
    p.add_argument("--heuristic-share", type=float, default=None)
    p.add_argument("--pool-cap", type=int, default=None)
    p.add_argument("--pool-slots", type=int, default=None,
                   help="pool snapshots played per iteration (one model + batcher each)")
    p.add_argument("--pool-sampling", default=None, choices=["pfsp", "uniform"],
                   help="pfsp (default medium): favour the snapshots the learner beats least, "
                        "weight max(1 - EMA win rate, --pfsp-floor); uniform: V1's draw")
    p.add_argument("--pfsp-floor", type=float, default=None,
                   help="smallest pfsp weight, so every snapshot keeps being played (0.1)")
    p.add_argument("--pool-ema", type=float, default=None,
                   help="weight of one training game in a snapshot's win-rate EMA (0.05)")
    p.add_argument("--snapshot-every", type=int, default=None)
    p.add_argument("--eval-every", type=int, default=None)
    p.add_argument("--eval-pool-battles", type=int, default=None)
    p.add_argument("--min-eval-battles", type=int, default=None,
                   help="finished eval battles needed before best.pt can change")
    p.add_argument("--confirm-battles", type=int, default=None,
                   help="re-evaluate a best.pt candidate over this many fresh battles and keep "
                        "it only if that confirmed win rate beats the best (default 400, 0 = off)")
    p.add_argument("--save-every", type=int, default=None)
    p.add_argument("--max-infer-batch", type=int, default=None,
                   help="largest rollout inference batch (default 64)")
    p.add_argument("--export-dir", default=None, help="default export/ (or the resumed run's)")
    p.add_argument("--elo-anchors", default=None,
                   help="fixed ratings of the scripted players, e.g. "
                        "random=0,maxpower=300,heuristic=600 (default: elo.ANCHORS)")
    p.add_argument("--elo-k", type=float, default=None, help="Elo k-factor per game (16)")
    p.set_defaults(fn=cmd_ppo)

    p = sub.add_parser("eval", help="greedy win rate (with a 95%% Wilson interval)")
    common(p)
    p.add_argument("--ckpt", required=True)
    p.add_argument("--battles", type=int, default=1000)
    p.add_argument("--opponent", default="heuristic", choices=["heuristic", "random", "maxpower"])
    p.add_argument("--vs-ckpt", default=None,
                   help="play another checkpoint instead (both greedy), e.g. V1 vs medium")
    p.set_defaults(fn=cmd_eval)

    p = sub.add_parser("elo", help="round-robin between checkpoints and scripted players")
    common(p)
    p.add_argument("--ckpts", nargs="*", default=[])
    p.add_argument("--scripted", nargs="*", default=["heuristic", "maxpower", "random"],
                   choices=list(SCRIPTED))
    p.add_argument("--battles", type=int, default=200, help="battles per pair")
    p.add_argument("--anchors", default=None,
                   help="fixed ratings, e.g. random=0 (default: elo.ANCHORS, for the scripted "
                        "players taking part)")
    p.add_argument("--out", default=None, help="write the results and ratings to this JSON file")
    p.set_defaults(fn=cmd_elo)
    return ap


def main() -> None:
    args = build_parser().parse_args()
    if args.cmd != "ppo" and args.concurrency is None:
        args.concurrency = 32
    args.fn(args)


if __name__ == "__main__":
    main()
