"""
Entry point.

    python train.py imitate --size medium --battles 3000 --epochs 8
    python train.py distill --teacher checkpoints/ppo_latest.pt --size medium --battles 3000
    python train.py ppo --init checkpoints/bc.pt --hours 24
    python train.py ppo --resume checkpoints/ppo_latest.pt
    python train.py eval --ckpt checkpoints/best.pt --battles 200

Needs one local Showdown server (see README), or --showdown-cmd to start it:
    node pokemon-showdown start --no-security

`--size small` keeps the MiniPokeV1 recipe (mirror self-play, constant LR and
entropy). `--size medium` turns on the long-run defaults: LR warmup + cosine,
entropy annealing, the opponent pool and a distributional value head.
"""

from __future__ import annotations

import argparse
import logging
import os
import random
import time
from pathlib import Path
from typing import Dict, Optional

os.environ.setdefault("PYTORCH_ENABLE_MPS_FALLBACK", "1")  # must precede torch import

import torch
from poke_env.player import MaxBasePowerPlayer, RandomPlayer

import runtime
from checkpoint import (load_checkpoint, rng_state, save_checkpoint, seed_everything,
                        set_rng_state)
from dataset import ShardDataset, ShardWriter
from export_onnx import export_checkpoint, random_inputs, INPUTS
from league import OpponentPool, split_battles
from model import (MiniPokeTransformer, ModelConfig, build_model, describe, pick_device, preset,
                   size_of)
from players import DistillPlayer, InferenceBatcher, TeacherPlayer
from ppo import PPOConfig, Schedule, distill_train, ppo_update
from rollout import (RolloutConfig, Rollouts, SafeHeuristicsPlayer, close_players, evaluate,
                     frozen_copy, load_into, make_kwargs, play_recording_pairs)
from runtime import (MetricsLogger, ShowdownServer, StopController, peak_rss_mb,
                     pick_infer_device, prune_checkpoints, rss_mb)

log = logging.getLogger("minipoke")

# Per-size defaults for every option left unset on the command line. "small"
# is exactly the MiniPokeV1 recipe; don't change it, or V1 stops reproducing.
DEFAULTS: Dict[str, Dict[str, object]] = {
    "small": dict(
        concurrency=32, battles_per_iter=128, lr=2e-4, warmup_iters=0, lr_min_ratio=1.0,
        ent_start=0.01, ent_end=0.01, mirror_share=1.0, pool_share=0.0, heuristic_share=0.0,
        bc_value_coef=0.0, vf_coef=0.5,
    ),
    "medium": dict(
        # lr 1e-4 / vf_coef 0.1: at 2e-4 / 0.5 the 51-bin value loss dominated the shared
        # trunk's gradient and PPO stalled around 43% vs the heuristic (55% with these)
        concurrency=48, battles_per_iter=192, lr=1e-4, warmup_iters=10, lr_min_ratio=0.1,
        ent_start=0.01, ent_end=0.003, mirror_share=0.7, pool_share=0.2, heuristic_share=0.1,
        bc_value_coef=0.5, vf_coef=0.1,
    ),
}
COMMON_DEFAULTS = dict(
    iterations=None, hours=None, minibatch=1024, eval_every=10, eval_battles=200,
    eval_pool_battles=100, min_eval_battles=150, snapshot_every=10, pool_cap=20, pool_slots=2,
    save_every=10, stall_timeout=180.0, infer_device="auto", seed=None,
    max_infer_batch=64,
)


def resolve(args: argparse.Namespace, size: str, keys) -> None:
    """Fill options left at None from the size's defaults."""
    for k in keys:
        if getattr(args, k, None) is None:
            setattr(args, k, DEFAULTS[size].get(k, COMMON_DEFAULTS.get(k)))


def setup(args, run_dir: Optional[Path] = None) -> torch.device:
    runtime.setup_logging(run_dir / "train.log" if run_dir else None)
    runtime.install_mps_fallback_warning()
    torch.set_num_threads(max(1, (os.cpu_count() or 4) // 2))
    return pick_device(args.device)


def sample_batch(n: int) -> Dict[str, torch.Tensor]:
    return dict(zip(INPUTS, random_inputs(n)))


def choose_infer_device(args, model, train_device) -> torch.device:
    if args.infer_device != "auto":
        return torch.device(args.infer_device)
    return pick_infer_device(model.to("cpu"), sample_batch, train_device, args.concurrency)


def server_for(args, run_dir: Optional[Path]) -> ShowdownServer:
    server = ShowdownServer(args.showdown_cmd, args.showdown_cwd, port=args.showdown_port,
                            log_path=(run_dir / "showdown.log") if run_dir else None)
    if not server.ensure_up():
        raise SystemExit("no Showdown server")
    return server


def new_model(args) -> MiniPokeTransformer:
    cfg = preset(args.size, dropout=args.dropout, value_bins=args.value_bins)
    return MiniPokeTransformer(cfg)


def eval_vs(model, infer_device, n: int, concurrency: int, opponent: str = "heuristic"):
    opp_cls = {"heuristic": SafeHeuristicsPlayer, "random": RandomPlayer,
               "maxpower": MaxBasePowerPlayer}[opponent]
    batcher = InferenceBatcher(frozen_copy(model, infer_device), infer_device,
                               max_batch=concurrency * 2)
    return evaluate(batcher, lambda: opp_cls(**make_kwargs(concurrency)), n, concurrency)


def eval_heuristic(model, infer_device, n: int, concurrency: int) -> float:
    wr, n_done = eval_vs(model, infer_device, n, concurrency)
    log.info("win rate vs SimpleHeuristicsPlayer: %.1f%% over %d battles", 100 * wr, n_done)
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
    log.info("[%s] dataset %s: %d samples (%d train / %d val), %.1f MB on disk "
             "(%.0f bytes/sample), process RSS %.0f MB",
             tag, data.root, len(data), data.n_samples("train"), data.n_samples("val"),
             data.bytes_on_disk() / 2**20, data.bytes_on_disk() / max(len(data), 1), rss_mb() or -1)
    model = model.to(device)
    log.info("[%s] model: %s on %s", tag, describe(model), device)
    runtime.mps_self_check(model, device, sample_batch(8))
    opt = torch.optim.AdamW(model.parameters(), lr=args.lr, weight_decay=1e-2)
    distill_train(model, opt, data, args.epochs, args.minibatch, device, vf_coef=vf_coef,
                  value_target=value_target, seed=args.seed or 0, log_prefix=f"[{tag}]")
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
    if not (root / "meta.json").exists():
        writer = ShardWriter(root, args.shard_size, info={"teacher": "SimpleHeuristicsPlayer",
                                                          "epsilon": args.epsilon})
        gamma = PPOConfig().gamma
        log.info("[bc] playing %d heuristic-vs-heuristic battles into %s...", args.battles, root)
        collect(args, writer, lambda: tuple(
            TeacherPlayer(writer, gamma, args.epsilon, **make_kwargs(args.concurrency))
            for _ in range(2)), server)
    else:
        log.info("[bc] reusing the dataset in %s", root)
    model = new_model(args)
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
    if not (root / "meta.json").exists():
        writer = ShardWriter(root, args.shard_size, info={"teacher": str(args.teacher),
                                                          "epsilon": args.epsilon})
        batcher = InferenceBatcher(frozen_copy(teacher, infer), infer,
                                   max_batch=args.concurrency * 2)
        gamma = PPOConfig().gamma
        log.info("[distill] playing %d teacher-vs-teacher battles into %s...", args.battles, root)
        collect(args, writer, lambda: tuple(
            DistillPlayer(batcher, writer, gamma, args.epsilon, **make_kwargs(args.concurrency))
            for _ in range(2)), server)
    else:
        log.info("[distill] reusing the dataset in %s", root)
    model = new_model(args)
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
           "clipfrac", "explained_var", "train_wr_pool", "train_wr_heuristic",
           "eval_wr_heuristic", "eval_n_heuristic", "eval_wr_pool", "eval_n_pool", "best_wr",
           "pool_size", "stalled", "rebuilds", "rss_mb"]


RUN_KEYS = ["run", "size", "ckpt_dir", "export_dir"]


def CKPT_DIR(args) -> Path:
    return Path(args.ckpt_dir or "checkpoints")


def cmd_ppo(args) -> None:
    resumed = load_checkpoint(args.resume) if args.resume else None
    if resumed:
        # Saved options, overridden by anything given on this command line
        for k, v in resumed["args"].items():
            if getattr(args, k, None) is None and k in PPO_KEYS + RUN_KEYS:
                setattr(args, k, v)
        # Keep writing where the run lives (checkpoints saved before these were stored)
        args.ckpt_dir = args.ckpt_dir or str(Path(args.resume).parent)
    args.export_dir = args.export_dir or "export"
    init = load_checkpoint(args.init) if args.init and not resumed else None
    if args.size is None and init is not None:
        # The defaults follow the model being trained, not the "small" fallback
        args.size = size_of(ModelConfig.from_dict(init["cfg"]))
    size = args.size or "small"
    args.size = size
    resolve(args, size, PPO_KEYS)
    if args.iterations is None and args.hours is None:
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
    model = model.to(device)
    cfg = PPOConfig(lr=args.lr, minibatch=args.minibatch, vf_coef=args.vf_coef)
    sched = Schedule(lr=args.lr, warmup_iters=args.warmup_iters, lr_min_ratio=args.lr_min_ratio,
                     ent_start=args.ent_start, ent_end=args.ent_end)
    opt = torch.optim.AdamW(model.parameters(), lr=cfg.lr, weight_decay=1e-2)
    pool = OpponentPool(ckpt_dir / "pool", cap=args.pool_cap)
    st = {"iteration": 0, "total_states": 0, "active_seconds": 0.0, "best_wr": -1.0,
          "best_iteration": None}
    if resumed:
        opt.load_state_dict(resumed["opt"])
        st.update(resumed["state"])
        pool.load_state_dict(resumed.get("pool"))
        set_rng_state(resumed.get("rng"))
        log.info("[ppo] resumed %s at iteration %d (%s states, %.2f h)", args.resume,
                 st["iteration"], f"{st['total_states']:,}", st["active_seconds"] / 3600)
    use_pool = args.pool_share > 0
    pool_rng = random.Random((args.seed or 0) + st["iteration"])

    log.info("[ppo] run %s, size %s: %s, train on %s", args.run, size, describe(model), device)
    log.info("[ppo] budget: %s iterations, %s hours; battles/iter %d (mirror %.0f%% / pool %.0f%% / "
             "heuristic %.0f%%), concurrency %d", args.iterations, args.hours, args.battles_per_iter,
             100 * args.mirror_share, 100 * args.pool_share, 100 * args.heuristic_share,
             args.concurrency)
    runtime.mps_self_check(model, device, sample_batch(8))
    infer_device = choose_infer_device(args, model, device)
    model.to(device)
    infer_model = frozen_copy(model, infer_device)
    rollouts = Rollouts(infer_model, infer_device, RolloutConfig(
        concurrency=args.concurrency, mirror_share=args.mirror_share, pool_share=args.pool_share,
        heuristic_share=args.heuristic_share, pool_slots=args.pool_slots,
        max_infer_batch=args.max_infer_batch, stall_timeout=args.stall_timeout), server)
    metrics = MetricsLogger(run_dir / "metrics.csv", METRICS)
    if use_pool and len(pool) == 0:
        pool.add(model, st["iteration"])  # the starting policy (BC / distill) is the first opponent

    def sync() -> None:
        load_into(infer_model, model.state_dict())

    def progress() -> float:
        p = 0.0
        if args.iterations:
            p = max(p, st["iteration"] / args.iterations)
        if args.hours:
            p = max(p, st["active_seconds"] / (3600 * args.hours))
        return p

    def save_all(periodic: bool) -> None:
        extra = dict(args={k: getattr(args, k) for k in PPO_KEYS + RUN_KEYS},
                     state=dict(st), pool=pool.state_dict(), rng=rng_state(),
                     iteration=st["iteration"])
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
        n_mirror, n_pool, n_heur = split_battles(args.battles_per_iter, args.mirror_share,
                                                 args.pool_share if use_pool else 0.0,
                                                 args.heuristic_share)
        if use_pool:
            rollouts.load_slots([pool.load_weights(s) for s in pool.sample(args.pool_slots, pool_rng)])
        res = rollouts.play_iteration(n_mirror, n_pool, n_heur)
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
                   stalled=int(not res.play.ok), rebuilds=rollouts.rebuilds,
                   **{k: stats[k] for k in ("pi_loss", "v_loss", "v_mse", "entropy", "kl",
                                            "clipfrac", "explained_var")})
        log.info("[ppo] it %4d  states %6d (total %s)  play %5.1fs  learn %4.1fs  %5.0f st/s  "
                 "lr %.2e  ent_c %.4f  pi %+.3f  v %.3f  ent %.2f  kl %.4f  clip %.3f  ev %.2f",
                 it, stats["samples"], f"{st['total_states']:,}", t_play, t_learn,
                 row["states_per_s"], lr, ent, stats["pi_loss"], stats["v_loss"],
                 stats["entropy"], stats["kl"], stats["clipfrac"], stats["explained_var"])

        if use_pool and it % args.snapshot_every == 0:
            pool.add(model, it)

        # ---- evaluate
        if it % args.eval_every == 0:
            sync()
            wr, n_done = rollouts.evaluate(args.eval_battles, "heuristic")
            row.update(eval_wr_heuristic=wr, eval_n_heuristic=n_done)
            msg = f"[ppo] it {it}: greedy win rate vs SimpleHeuristicsPlayer = {wr:.1%} ({n_done})"
            if use_pool:
                wins = total = 0
                for snap in [s for s in pool.recent(4) if s.iteration != it][:3]:
                    pwr, pn = rollouts.evaluate(args.eval_pool_battles, "pool", pool.load_weights(snap))
                    wins += pwr * pn
                    total += pn
                    msg += f", vs it {snap.iteration} = {pwr:.1%}"
                if total:
                    row.update(eval_wr_pool=wins / total, eval_n_pool=total)
            log.info(msg)
            if n_done >= args.min_eval_battles and wr > st["best_wr"]:
                st["best_wr"], st["best_iteration"] = wr, it
                save_checkpoint(ckpt_dir / "best.pt", model, None, iteration=it, eval_wr_heuristic=wr)
                log.info("[ppo] new best: %.1f%% at iteration %d -> %s", 100 * wr, it,
                         ckpt_dir / "best.pt")

        st["active_seconds"] += time.time() - t_iter
        row.update(hours=st["active_seconds"] / 3600, best_wr=max(st["best_wr"], 0.0),
                   rss_mb=rss_mb())
        metrics.append(row)
        save_all(periodic=it % args.save_every == 0)

    # ---- end of run
    save_all(periodic=True)
    log.info("[ppo] stopped at iteration %d (%s): %s states in %.2f h, peak RSS %.0f MB",
             st["iteration"], stop.reason or "budget reached", f"{st['total_states']:,}",
             st["active_seconds"] / 3600, peak_rss_mb() or -1)
    close_players(rollouts.players)
    best = ckpt_dir / "best.pt"
    export_checkpoint(best if best.exists() else ckpt_dir / "ppo_latest.pt", args.export_dir)
    server.stop()


def cmd_eval(args) -> None:
    setup(args)
    infer = torch.device(args.infer_device or "cpu")
    model = build_model(load_checkpoint(args.ckpt))
    log.info("%s: %s", args.ckpt, describe(model))
    server = server_for(args, None)
    wr, n = eval_vs(model, infer, args.battles, args.concurrency, args.opponent)
    server.stop()
    print(f"win rate vs {args.opponent}: {wr:.1%} over {n} battles")


def main() -> None:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)

    def common(p):
        p.add_argument("--device", default="auto", help="training device: auto|mps|cuda|cpu")
        p.add_argument("--infer-device", default=None,
                       help="rollout inference device; auto (default for ppo) benchmarks CPU "
                            "against the training device and keeps the faster")
        p.add_argument("--concurrency", type=int, default=None,
                       help="concurrent battles per seat pair (default 32 small, 48 medium)")
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
        p.add_argument("--size", default=default_size, choices=["small", "medium"])
        p.add_argument("--dropout", type=float, default=None)
        p.add_argument("--value-bins", type=int, default=None,
                       help="1 = scalar value head; default 1 small, 51 medium")

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
                   help="also fit the value head to battle returns (default 0 small, 0.5 medium)")
    p.set_defaults(fn=cmd_imitate)

    p = sub.add_parser("distill", help="clone a trained model's full action distribution")
    common(p)
    model_args(p, "medium")
    data_args(p, 3000, 0.05)
    p.add_argument("--teacher", required=True)
    p.add_argument("--vf-coef", type=float, default=0.5)
    p.add_argument("--value-target", default="teacher", choices=["teacher", "return"])
    p.set_defaults(fn=cmd_distill)

    p = sub.add_parser("ppo")
    common(p)
    p.set_defaults(eval_battles=None, stall_timeout=None)
    p.add_argument("--size", default=None, choices=["small", "medium"],
                   help="for a new model and its defaults; --init/--resume keep their architecture")
    p.add_argument("--dropout", type=float, default=None)
    p.add_argument("--value-bins", type=int, default=None)
    p.add_argument("--init", default=None, help="checkpoint to start from (e.g. checkpoints/bc.pt)")
    p.add_argument("--resume", default=None, help="continue a run (e.g. checkpoints/ppo_latest.pt)")
    p.add_argument("--run", default=None, help="name under runs/ (metrics.csv, train.log)")
    p.add_argument("--iterations", type=int, default=None,
                   help="total iteration budget (default 300 when --hours is not set)")
    p.add_argument("--hours", type=float, default=None,
                   help="total training-time budget; resuming counts the time already spent")
    p.add_argument("--battles-per-iter", type=int, default=None)
    p.add_argument("--minibatch", type=int, default=None)
    p.add_argument("--lr", type=float, default=None, help="peak learning rate")
    p.add_argument("--vf-coef", type=float, default=None,
                   help="value loss weight (default 0.5 small, 0.1 medium)")
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
    p.add_argument("--snapshot-every", type=int, default=None)
    p.add_argument("--eval-every", type=int, default=None)
    p.add_argument("--eval-pool-battles", type=int, default=None)
    p.add_argument("--min-eval-battles", type=int, default=None,
                   help="finished eval battles needed before best.pt can change")
    p.add_argument("--save-every", type=int, default=None)
    p.add_argument("--max-infer-batch", type=int, default=None,
                   help="largest rollout inference batch (default 64)")
    p.add_argument("--export-dir", default=None, help="default export/ (or the resumed run's)")
    p.set_defaults(fn=cmd_ppo)

    p = sub.add_parser("eval")
    common(p)
    p.add_argument("--ckpt", required=True)
    p.add_argument("--battles", type=int, default=200)
    p.add_argument("--opponent", default="heuristic", choices=["heuristic", "random", "maxpower"])
    p.set_defaults(fn=cmd_eval)

    args = ap.parse_args()
    if args.cmd != "ppo" and args.concurrency is None:
        args.concurrency = 32
    args.fn(args)


if __name__ == "__main__":
    main()
