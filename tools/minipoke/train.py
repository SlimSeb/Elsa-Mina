"""
Entry point.

    python train.py imitate --battles 3000 --epochs 8
    python train.py ppo --init checkpoints/bc.pt --iterations 300
    python train.py eval --ckpt checkpoints/ppo_latest.pt --battles 200

Needs one local Showdown server (see README):
    node pokemon-showdown start --no-security
"""

from __future__ import annotations

import argparse
import asyncio
import copy
import logging
import os
import threading
import time
from pathlib import Path

os.environ.setdefault("PYTORCH_ENABLE_MPS_FALLBACK", "1")  # must precede torch import

import torch
from poke_env.player import MaxBasePowerPlayer, RandomPlayer, SimpleHeuristicsPlayer

from model import MiniPokeTransformer, ModelConfig, count_params, pick_device
from players import EpisodeCollector, InferenceBatcher, RLPlayer, TeacherPlayer
from ppo import PPOConfig, bc_train, ppo_update

FORMAT = "gen9randombattle"
CKPT_DIR = Path("checkpoints")


def make_kwargs(concurrency: int) -> dict:
    return dict(
        battle_format=FORMAT,
        max_concurrent_battles=concurrency,
        log_level=logging.WARNING,
        open_timeout=None,
    )


def save(model, opt, path: Path, extra: dict | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    torch.save({"model": model.state_dict(), "cfg": model.cfg.__dict__,
                "opt": opt.state_dict() if opt else None, **(extra or {})}, path)


def load_model(path: str | None, device) -> MiniPokeTransformer:
    if path:
        ck = torch.load(path, map_location="cpu")
        model = MiniPokeTransformer(ModelConfig(**ck["cfg"]))
        model.load_state_dict(ck["model"])
    else:
        model = MiniPokeTransformer()
    return model.to(device)


def inference_copy(model, infer_device) -> MiniPokeTransformer:
    m = copy.deepcopy(model).to(infer_device).eval()
    for p in m.parameters():
        p.requires_grad_(False)
    return m


def sync(src, dst) -> None:
    dst.load_state_dict({k: v.to(next(dst.parameters()).device) for k, v in src.state_dict().items()})


# --------------------------------------------------------------------------
async def evaluate(batcher, opponent: str, n_battles: int, concurrency: int) -> float:
    me = RLPlayer(batcher, collector=None, greedy=True, **make_kwargs(concurrency))
    opp_cls = {"heuristic": SimpleHeuristicsPlayer, "random": RandomPlayer,
               "maxpower": MaxBasePowerPlayer}[opponent]
    opp = opp_cls(**make_kwargs(concurrency))
    await me.battle_against(opp, n_battles=n_battles)
    return me.n_won_battles / max(me.n_finished_battles, 1)


def cmd_imitate(args) -> None:
    device = pick_device(args.device)
    dataset, lock = [], threading.Lock()
    p1 = TeacherPlayer(dataset, lock, epsilon=args.epsilon, **make_kwargs(args.concurrency))
    p2 = TeacherPlayer(dataset, lock, epsilon=args.epsilon, **make_kwargs(args.concurrency))

    t0 = time.time()
    print(f"[bc] playing {args.battles} heuristic-vs-heuristic battles...")
    asyncio.run(p1.battle_against(p2, n_battles=args.battles))
    print(f"[bc] {len(dataset)} samples in {time.time() - t0:.0f}s")

    model = load_model(None, device)
    print(f"[bc] model: {count_params(model) / 1e6:.2f}M params on {device}")
    opt = torch.optim.AdamW(model.parameters(), lr=args.lr, weight_decay=1e-2)
    bc_train(model, opt, dataset, args.epochs, args.minibatch, device)
    save(model, None, CKPT_DIR / "bc.pt")

    batcher = InferenceBatcher(inference_copy(model, torch.device(args.infer_device)),
                               torch.device(args.infer_device))
    wr = asyncio.run(evaluate(batcher, "heuristic", args.eval_battles, args.concurrency))
    print(f"[bc] win rate vs SimpleHeuristicsPlayer: {wr:.1%}")


def cmd_ppo(args) -> None:
    device = pick_device(args.device)
    infer_device = torch.device(args.infer_device)
    model = load_model(args.init, device)
    print(f"[ppo] model: {count_params(model) / 1e6:.2f}M params, "
          f"train on {device}, rollouts on {infer_device}")
    cfg = PPOConfig(lr=args.lr, minibatch=args.minibatch)
    opt = torch.optim.AdamW(model.parameters(), lr=cfg.lr, weight_decay=1e-2)

    infer_model = inference_copy(model, infer_device)
    batcher = InferenceBatcher(infer_model, infer_device, max_batch=args.concurrency * 2)
    collector = EpisodeCollector()
    # Two seats sharing one policy = self-play. Both sides' experience is used.
    p1 = RLPlayer(batcher, collector, **make_kwargs(args.concurrency))
    p2 = RLPlayer(batcher, collector, **make_kwargs(args.concurrency))

    total_states = 0
    for it in range(1, args.iterations + 1):
        sync(model, infer_model)
        t0 = time.time()
        asyncio.run(p1.battle_against(p2, n_battles=args.battles_per_iter))
        p1.reset_battles()
        p2.reset_battles()
        episodes = collector.drain()
        t_play = time.time() - t0

        t0 = time.time()
        stats = ppo_update(model, opt, episodes, cfg, device)
        t_learn = time.time() - t0
        total_states += stats["samples"]
        print(f"[ppo] it {it:4d}  states {stats['samples']:6d} (total {total_states:,})  "
              f"play {t_play:5.1f}s  learn {t_learn:4.1f}s  "
              f"{stats['samples'] / (t_play + t_learn):6.0f} st/s  "
              f"pi {stats['pi_loss']:+.3f}  v {stats['v_loss']:.3f}  "
              f"ent {stats['entropy']:.2f}  kl {stats['kl']:.4f}")

        if it % args.eval_every == 0 or it == args.iterations:
            sync(model, infer_model)
            wr = asyncio.run(evaluate(batcher, "heuristic", args.eval_battles, args.concurrency))
            print(f"[ppo] it {it}: win rate vs SimpleHeuristicsPlayer = {wr:.1%}")
            save(model, opt, CKPT_DIR / "ppo_latest.pt", {"iteration": it})
            save(model, opt, CKPT_DIR / f"ppo_{it:05d}.pt", {"iteration": it})


def cmd_eval(args) -> None:
    infer_device = torch.device(args.infer_device)
    model = load_model(args.ckpt, infer_device)
    batcher = InferenceBatcher(inference_copy(model, infer_device), infer_device)
    wr = asyncio.run(evaluate(batcher, args.opponent, args.battles, args.concurrency))
    print(f"win rate vs {args.opponent}: {wr:.1%} over {args.battles} battles")


def main() -> None:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)

    def common(p):
        p.add_argument("--device", default="auto", help="training device: auto|mps|cuda|cpu")
        p.add_argument("--infer-device", default="cpu",
                       help="rollout inference device (cpu is usually fastest for small batches)")
        p.add_argument("--concurrency", type=int, default=32,
                       help="concurrent battles per player on the single Showdown server")
        p.add_argument("--eval-battles", type=int, default=200)

    p = sub.add_parser("imitate")
    common(p)
    p.add_argument("--battles", type=int, default=3000)
    p.add_argument("--epochs", type=int, default=8)
    p.add_argument("--minibatch", type=int, default=512)
    p.add_argument("--lr", type=float, default=5e-4)
    p.add_argument("--epsilon", type=float, default=0.1)
    p.set_defaults(fn=cmd_imitate)

    p = sub.add_parser("ppo")
    common(p)
    p.add_argument("--init", default=None, help="checkpoint to start from (e.g. checkpoints/bc.pt)")
    p.add_argument("--iterations", type=int, default=300)
    p.add_argument("--battles-per-iter", type=int, default=128)
    p.add_argument("--minibatch", type=int, default=1024)
    p.add_argument("--lr", type=float, default=2e-4)
    p.add_argument("--eval-every", type=int, default=10)
    p.set_defaults(fn=cmd_ppo)

    p = sub.add_parser("eval")
    common(p)
    p.add_argument("--ckpt", required=True)
    p.add_argument("--battles", type=int, default=200)
    p.add_argument("--opponent", default="heuristic", choices=["heuristic", "random", "maxpower"])
    p.set_defaults(fn=cmd_eval)

    args = ap.parse_args()
    torch.set_num_threads(max(1, (os.cpu_count() or 4) // 2))
    args.fn(args)


if __name__ == "__main__":
    main()
