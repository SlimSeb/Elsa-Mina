"""
Entry point: offline RL on human replays, and the measurements.

    python train.py offline --data data/human-v21 --size wide-medium --algo awbc
    python train.py offline-eval --ckpt checkpoints/v3/awbc-4m/offline.pt ../../src/ElsaMina.Battles/Models/MiniPokeV3_22M \
        --data data/human-v21
    python train.py eval --ckpt checkpoints/v3/awbc-4m/offline.pt
    python train.py eval --ckpt checkpoints/v3/awbc-4m/offline.pt --vs-ckpt ../../src/ElsaMina.Battles/Models/MiniPokeV3_22M
    python train.py eval --ckpt checkpoints/v3/awbc-4m/offline.pt --search --processes 4
    python train.py elo --ckpts a.pt b.pt --scripted heuristic maxpower random

`offline` and `offline-eval` read a human_data.py dataset (see offline.py). `eval` and `elo` play
games on one local Showdown server (see README), or --showdown-cmd starts it:
    node pokemon-showdown start --no-security
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import random
import subprocess
from pathlib import Path
from typing import Dict, Optional

os.environ.setdefault("PYTORCH_ENABLE_MPS_FALLBACK", "1")  # must precede torch import

import torch

import runtime
from checkpoint import load_checkpoint, save_checkpoint, seed_everything
from elo import fit_ratings, parse_anchors, round_robin_pairs
from encoder_v2 import model_encoder
from export_onnx import model_inputs, random_inputs
from matches import SCRIPTED, MatchResult, evaluate, frozen_copy, make_kwargs
from model import PRESETS, MiniPokeTransformer, build_model, describe, pick_device, preset
from paired import TeamPairs, summarize
from players import InferenceBatcher, ModelPlayer
from runtime import ShowdownServer, peak_rss_mb
from setpriors import SetPriors

log = logging.getLogger("minipoke")


def setup(args, run_dir: Optional[Path] = None) -> torch.device:
    runtime.setup_logging(run_dir / "train.log" if run_dir else None)
    runtime.install_mps_fallback_warning()
    torch.set_num_threads(max(1, (os.cpu_count() or 4) // 2))
    return pick_device(args.device)


def sample_batch(n: int, model) -> Dict[str, torch.Tensor]:
    return dict(zip(model_inputs(model), random_inputs(n, model=model)))


def load_policy(path):
    """A checkpoint, or a released model's folder (src/ElsaMina.Battles/Models/<name>: its
    minipoke.onnx and encoder), as a policy that plays and evaluates like a model."""
    from offline import OnnxPolicy, is_onnx_folder
    return OnnxPolicy(path) if is_onnx_folder(path) else build_model(load_checkpoint(path))


def describe_policy(model) -> str:
    return model.describe() if hasattr(model, "session") else describe(model)


def server_for(args, run_dir: Optional[Path]) -> ShowdownServer:
    server = ShowdownServer(args.showdown_cmd, args.showdown_cwd, port=args.showdown_port,
                            log_path=(run_dir / "showdown.log") if run_dir else None)
    if not server.ensure_up():
        raise SystemExit("no Showdown server")
    return server


def eval_vs(model, infer_device, n: int, concurrency: int, opponent: str = "heuristic",
            no_tera_before: int = 0, pairs: Optional[TeamPairs] = None,
            search: Optional["SearchSetup"] = None) -> MatchResult:
    opp_cls = SCRIPTED[opponent]
    batcher = InferenceBatcher(frozen_copy(model, infer_device), infer_device,
                               max_batch=concurrency * 2)
    return evaluate(batcher, lambda: opp_cls(**make_kwargs(concurrency)), n, concurrency,
                    no_tera_before=no_tera_before, pairs=pairs,
                    player=search.player(batcher, concurrency) if search else None)


def head_to_head(model, other, infer_device, n: int, concurrency: int, no_tera_before: int = 0,
                 pairs: Optional[TeamPairs] = None, search: Optional["SearchSetup"] = None,
                 vs_search: Optional["SearchSetup"] = None) -> MatchResult:
    """Greedy `model` (with `search`: searching) vs greedy `other` (with `vs_search`:
    searching), `model`'s results."""
    batcher, other_batcher = (InferenceBatcher(frozen_copy(m, infer_device), infer_device,
                                               max_batch=concurrency * 2) for m in (model, other))
    if vs_search:
        make_other = lambda: vs_search.player(other_batcher, concurrency)
    else:
        make_other = lambda: ModelPlayer(other_batcher, **make_kwargs(concurrency))
    return evaluate(batcher, make_other, n, concurrency, no_tera_before=no_tera_before, pairs=pairs,
                    player=search.player(batcher, concurrency) if search else None)


def search_config(settings, depth: int):
    """The SearchConfig of `--search-*` settings (args or an eval spec dict), `depth` deep."""
    from search import SearchConfig
    get = settings.get if isinstance(settings, dict) else lambda key: getattr(settings, key)
    return SearchConfig(top_k=get("search_top_k"), min_prob=get("search_min_prob"),
                        determinizations=get("search_determinizations"), samples=get("search_samples"),
                        faint_reward=get("search_faint_reward"), depth=depth)


class SearchSetup:
    """`--search`: the --ckpt player searches one turn ahead (search.py); `--vs-search-depth`:
    the --vs-ckpt player too."""

    def __init__(self, args, depth: Optional[int] = None):
        from search import SimPool
        self.config = search_config(args, args.search_depth if depth is None else depth)
        self.sims = SimPool(args.showdown_cwd, args.search_workers)
        self.players = []

    def player(self, batcher, concurrency: int):
        from search import SearchPlayer
        player = SearchPlayer(batcher, self.sims, self.config, **make_kwargs(concurrency))
        self.players.append(player)
        return player

    def describe(self) -> str:
        return "; ".join(p.stats.describe() for p in self.players)


def team_pairs(args, battles: int) -> Optional[TeamPairs]:
    """The team pairs of a paired evaluation (`battles` games, two per pair), or None
    for `--unpaired` or when the teams can't be generated (no Node or Showdown clone)."""
    if args.unpaired:
        return None
    seed = args.team_seed if args.team_seed is not None else (args.seed or 0)
    try:
        return TeamPairs.generate(max(1, battles // 2), seed, args.showdown_cwd)
    except (OSError, subprocess.CalledProcessError) as exc:
        log.warning("paired teams unavailable (%s): independent random battles instead", exc)
        return None


# --------------------------------------------------------------------------
# Offline RL on human replays (offline.py, data from human_data.py)
# --------------------------------------------------------------------------
def offline_model(args, data) -> MiniPokeTransformer:
    """--init's model, or a new --size model reading the dataset's observation (its encoder
    flags and set priors), checked against the dataset's layout."""
    flags = {k: v for k, v in (data.meta.get("model_flags") or {}).items()}
    if args.init:
        model = build_model(load_checkpoint(args.init))
    else:
        cfg = preset(args.size, dropout=args.dropout, value_bins=args.value_bins,
                     move_tokens=args.move_tokens or None, typed_ffn=args.typed_ffn or None, **flags)
        model = MiniPokeTransformer(cfg)
        if data.meta.get("set_priors"):
            model.priors = SetPriors.from_dict(data.meta["set_priors"])
    if model_encoder(model).layout != data.obs_layout:
        raise SystemExit(f"{args.data} holds observations of another encoder than the model's "
                         f"(dataset flags {flags}): rebuild it with human_data.py --encoder-from")
    theirs = data.meta.get("set_priors")
    if theirs and model.priors is not None and SetPriors.from_dict(theirs).sha256 != model.priors.sha256:
        log.warning("[offline] the dataset was encoded with other set priors than the model's")
    return model


def cmd_offline(args) -> None:
    from offline import ALGOS, HumanData, OfflineConfig, describe_metrics, offline_metrics, offline_train
    resumed = load_checkpoint(args.resume) if args.resume else None
    if resumed is not None and not (resumed.get("offline") or {}).get("config"):
        raise SystemExit(f"{args.resume} is not a checkpoint of train.py offline")
    out = Path(args.ckpt_dir or (Path(args.resume).parent if resumed else f"checkpoints/human-{args.algo}"))
    device = setup(args, Path("runs") / (args.run or out.name))
    seed_everything(args.seed or 0)
    data = HumanData(args.data)
    if resumed:
        # The run's own model and settings: the command line's training options are not used
        saved = resumed["offline"]
        if Path(saved["data"]).resolve() != Path(args.data).resolve():
            log.warning("[offline] %s was trained on %s, not %s", args.resume, saved["data"], args.data)
        model = build_model(resumed).to(device)
        cfg = OfflineConfig(**{k: v for k, v in saved["config"].items()
                               if k in OfflineConfig.__dataclass_fields__})
        if cfg.algo not in ALGOS:
            raise SystemExit(f"{args.resume} was trained with --algo {cfg.algo}, which is gone: "
                             f"start a new run with one of {', '.join(ALGOS)}")
    else:
        model = offline_model(args, data).to(device)
        cfg = OfflineConfig(algo=args.algo, weight=args.weight, beta=args.beta, max_weight=args.max_weight,
                            warmup_epochs=args.warmup_epochs, gamma=data.meta.get("gamma", 0.995),
                            lam=args.gae_lambda, vf_coef=args.vf_coef,
                            value_ce_norm=args.value_ce_norm == "log-bins", epochs=args.epochs,
                            minibatch=args.minibatch, lr=args.lr, lr_min_ratio=args.lr_min_ratio,
                            warmup_steps=args.warmup_steps, patience=args.patience,
                            min_rating=args.min_rating, train_min_rating=args.train_min_rating)
    log.info("[offline] model: %s on %s; data %s: %s", describe(model), device, args.data,
             {k: len(v) for k, v in data.splits.items()})
    runtime.mps_self_check(model, device, sample_batch(8, model))

    def save(path, m, optimizer, **extra):
        save_checkpoint(path, m, optimizer, **extra)

    result = offline_train(model, data, cfg, device, out, save, seed=args.seed or 0, log=log.info,
                           resume={**resumed["offline"], "opt": resumed.get("opt")} if resumed else None)
    best = build_model(load_checkpoint(out / "offline.pt")).to(device)
    metrics = offline_metrics(best, data["test"], device, args.min_rating)
    log.info("[offline] test battles, best epoch (%d):\n%s", result["best_epoch"], describe_metrics(metrics))
    (out / "offline_metrics.json").write_text(json.dumps({**result, "test": metrics, "args": {
        k: v for k, v in vars(args).items() if k != "fn"}}, indent=1, default=str))
    log.info("[offline] wrote %s; peak RSS %.0f MB", out / "offline_metrics.json", peak_rss_mb() or -1)


def cmd_offline_eval(args) -> None:
    from offline import HumanData, describe_metrics, offline_metrics
    from encoder_v2 import model_encoder as encoder_of
    device = setup(args)
    data = HumanData(args.data)
    results = {}
    from offline import OnnxPolicy, is_onnx_folder
    for path in args.ckpt:
        model = OnnxPolicy(path, threads=4) if is_onnx_folder(path) else load_policy(path)
        on = torch.device("cpu") if hasattr(model, "session") else device
        model = model.to(on)
        if encoder_of(model).layout != data.obs_layout:
            print(f"{path}: reads another observation than {args.data}: skipped")
            continue
        metrics = offline_metrics(model, data[args.split], on, args.min_rating)
        results[str(path)] = metrics
        print(f"{path} ({describe_policy(model)}), {args.split} battles:\n{describe_metrics(metrics)}")
    if args.out:
        Path(args.out).parent.mkdir(parents=True, exist_ok=True)
        Path(args.out).write_text(json.dumps(results, indent=1))
        print(f"wrote {args.out}")


# --------------------------------------------------------------------------
# Games: eval and elo
# --------------------------------------------------------------------------
EVAL_SPEC = ("ckpt", "vs_ckpt", "opponent", "concurrency", "no_tera_before", "stall_timeout",
             "search", "search_top_k", "search_min_prob", "search_determinizations", "search_samples",
             "search_workers", "search_faint_reward", "search_depth", "vs_search_depth", "showdown_cwd")


def _eval_worker(spec: dict) -> dict:
    """One process of `eval --processes`: its share of the team pairs."""
    import random as _random
    from poke_env import AccountConfiguration
    from matches import run_match
    logging.basicConfig(level=logging.WARNING)
    torch.set_num_threads(1)
    infer = torch.device("cpu")
    tag = f"{spec['tag']}{spec['worker']}"
    account = lambda side: AccountConfiguration(f"Ev{tag}{side}", None)
    kwargs = lambda side: dict(account_configuration=account(side), **make_kwargs(spec["concurrency"]))
    model = load_policy(spec["ckpt"])
    batcher = InferenceBatcher(frozen_copy(model, infer), infer, max_batch=spec["concurrency"] * 2)
    pools, searchers = [], []

    def searching(searcher_batcher, depth: int, side: str):
        from search import SearchPlayer, SimPool
        pools.append(SimPool(spec["showdown_cwd"], spec["search_workers"]))
        searchers.append(SearchPlayer(searcher_batcher, pools[-1], search_config(spec, depth), **kwargs(side)))
        searchers[-1]._seed = iter(range(_random.randrange(1 << 20), 1 << 30))
        return searchers[-1]

    if spec["search"]:
        player = searching(batcher, spec["search_depth"], "A")
    else:
        player = ModelPlayer(batcher, no_tera_before=spec["no_tera_before"], **kwargs("A"))
    if spec["vs_ckpt"]:
        other = load_policy(spec["vs_ckpt"])
        other_batcher = InferenceBatcher(frozen_copy(other, infer), infer, max_batch=spec["concurrency"] * 2)
        if spec["vs_search_depth"]:
            opponent = searching(other_batcher, spec["vs_search_depth"], "B")
        else:
            opponent = ModelPlayer(other_batcher, **kwargs("B"))
    else:
        opponent = SCRIPTED[spec["opponent"]](**kwargs("B"))
    result = run_match(player, opponent, 0, spec["stall_timeout"], None,
                       TeamPairs(spec["first"], spec["second"]), "evaluation")
    for pool in pools:
        pool.close()
    return {"scores": result.paired.scores, "games": result.games, "ties": result.ties,
            "misplaced": result.paired.misplaced,
            "search": " | ".join(p.stats.describe() for p in searchers)}


def eval_in_processes(args, pairs: TeamPairs) -> None:
    """`eval --processes P`: P processes play a share of the team pairs each, on the same
    server; the pair scores are pooled."""
    import multiprocessing
    from concurrent.futures import ProcessPoolExecutor
    n, workers = len(pairs), args.processes
    spec = {k: getattr(args, k) for k in EVAL_SPEC}
    spec["tag"] = random.randrange(1000, 10000)
    bounds = [(n * i // workers, n * (i + 1) // workers) for i in range(workers)]
    with ProcessPoolExecutor(workers, mp_context=multiprocessing.get_context("spawn")) as pool:
        outs = list(pool.map(_eval_worker, [
            {**spec, "worker": i, "first": pairs.first[a:b], "second": pairs.second[a:b]}
            for i, (a, b) in enumerate(bounds)]))
    result = summarize([s for o in outs for s in o["scores"]], sum(o["games"] for o in outs),
                       sum(o["ties"] for o in outs), sum(o["misplaced"] for o in outs))
    label = vs_label(args) if args.vs_ckpt else args.opponent
    print(f"win rate{with_search(args)} vs {label}: {result.describe()} ({workers} processes)")
    for o in outs:
        if o["search"]:
            print(f"search: {o['search']}")


def cmd_eval(args) -> None:
    setup(args)
    if args.processes > 1:
        server = server_for(args, None)
        pairs = team_pairs(args, args.battles)
        if pairs is None:
            raise SystemExit("--processes plays paired teams: they could not be generated")
        eval_in_processes(args, pairs)
        server.stop()
        return
    infer = torch.device(args.infer_device or "cpu")
    model = load_policy(args.ckpt)
    log.info("%s: %s", args.ckpt, describe_policy(model))
    server = server_for(args, None)
    pairs = team_pairs(args, args.battles)
    search = SearchSetup(args) if args.search else None
    vs_search = SearchSetup(args, args.vs_search_depth) if args.vs_ckpt and args.vs_search_depth else None
    if args.vs_ckpt:
        other = load_policy(args.vs_ckpt)
        log.info("%s: %s", args.vs_ckpt, describe_policy(other))
        result = head_to_head(model, other, infer, args.battles, args.concurrency,
                              args.no_tera_before, pairs, search, vs_search)
        label = vs_label(args)
    else:
        result = eval_vs(model, infer, args.battles, args.concurrency, args.opponent,
                         args.no_tera_before, pairs, search)
        label = args.opponent
    server.stop()
    restricted = f" (no tera before turn {args.no_tera_before})" if args.no_tera_before else ""
    print(f"win rate{with_search(args)}{restricted} vs {label}: {result.describe()}")
    for setup_ in (search, vs_search):
        if setup_:
            print(f"search: {setup_.describe()}")
            setup_.sims.close()


def with_search(args) -> str:
    if not args.search:
        return ""
    return " with search" + (f" (depth {args.search_depth})" if args.search_depth > 1 else "")


def vs_label(args) -> str:
    if args.vs_search_depth:
        return f"{args.vs_ckpt} (searching, depth {args.vs_search_depth})"
    return f"{args.vs_ckpt} (greedy)"


def _matchup_worker(spec: dict) -> dict:
    """One match of `elo`: two players ("ckpt", path) or ("scripted", name), on the
    match's team pairs, in this process or one of `--processes`."""
    from poke_env import AccountConfiguration
    from matches import run_match
    torch.set_num_threads(1)
    infer = torch.device("cpu")

    sims = []

    def player(kind: str, what: str, side: str):
        kwargs = dict(account_configuration=AccountConfiguration(f"Elo{spec['tag']}{spec['index']}{side}", None),
                      **make_kwargs(spec["concurrency"]))
        if kind == "scripted":
            return SCRIPTED[what](**kwargs)
        model = load_policy(what)
        batcher = InferenceBatcher(frozen_copy(model, infer), infer, max_batch=spec["concurrency"] * 2)
        if kind == "search":
            from search import SearchConfig, SearchPlayer, SimPool
            sims.append(SimPool(spec["showdown_cwd"], spec["search_workers"]))
            return SearchPlayer(batcher, sims[-1], SearchConfig(faint_reward=spec["search_faint_reward"]),
                                **kwargs)
        return ModelPlayer(batcher, **kwargs)

    pairs = TeamPairs(spec["first"], spec["second"]) if spec["first"] else None
    result = run_match(player(*spec["a"], "A"), player(*spec["b"], "B"), spec["battles"],
                       spec["stall_timeout"], None, pairs, "match")
    for pool in sims:
        pool.close()
    return {"score": result.score, "games": result.games, "ties": result.ties,
            "describe": result.describe()}


def cmd_elo(args) -> None:
    """Round-robin between checkpoints and scripted players, rated by maximum
    likelihood with the scripted players as anchors."""
    setup(args)
    players = {}
    for path in args.ckpts:
        name = Path(path).stem if sum(Path(p).stem == Path(path).stem for p in args.ckpts) == 1 \
            else str(path)
        name = Path(path).name if Path(path).is_dir() else name   # a released model's folder
        log.info("%s: %s", name, describe_policy(load_policy(path)))
        players[name] = ("ckpt", path)
    for path in args.search_ckpts:
        players[f"{Path(path).stem}+search"] = ("search", path)
    for name in args.scripted:
        players[name] = ("scripted", name)
    anchors = {k: v for k, v in parse_anchors(args.anchors).items() if k in players}
    server = server_for(args, None)
    pairs = team_pairs(args, args.battles)   # every match plays the same team pairs
    matchups = round_robin_pairs(players)
    tag = random.randrange(1000, 10000)
    specs = [{"a": players[a], "b": players[b], "index": i, "tag": tag, "battles": args.battles,
              "concurrency": args.concurrency, "stall_timeout": args.stall_timeout,
              "showdown_cwd": args.showdown_cwd, "search_workers": args.search_workers,
              "search_faint_reward": args.search_faint_reward,
              "first": pairs.first if pairs else None, "second": pairs.second if pairs else None}
             for i, (a, b) in enumerate(matchups)]
    if args.processes > 1:
        import multiprocessing
        from concurrent.futures import ProcessPoolExecutor
        pool = ProcessPoolExecutor(args.processes, mp_context=multiprocessing.get_context("spawn"))
        outcomes = pool.map(_matchup_worker, specs)
    else:
        pool, outcomes = None, map(_matchup_worker, specs)
    results = {}
    for (a, b), out in zip(matchups, outcomes):   # in order, as they finish
        results[(a, b)] = (out["score"], out["games"])
        print(f"{a} vs {b}: {out['describe']}", flush=True)
    if pool is not None:
        pool.shutdown()
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
        p.add_argument("--device", default="auto", help="training / inference device: auto|mps|cuda|cpu")
        p.add_argument("--seed", type=int, default=None)

    def games(p):
        """The options of the commands that play games (eval, elo)."""
        p.add_argument("--infer-device", default=None, help="inference device of the players (default cpu)")
        p.add_argument("--concurrency", type=int, default=32, help="concurrent battles per seat pair")
        p.add_argument("--stall-timeout", type=float, default=180.0,
                       help="end a match if no battle finishes for this long (s)")
        p.add_argument("--showdown-cmd", default=None,
                       help='(re)start the server when it is down, e.g. '
                            '"node pokemon-showdown start --no-security"')
        p.add_argument("--showdown-cwd", default="pokemon-showdown")
        p.add_argument("--showdown-port", type=int, default=8000)
        p.add_argument("--unpaired", action="store_true",
                       help="independent random battles instead of paired teams "
                            "(each team pair played twice, swapped; see paired.py)")
        p.add_argument("--team-seed", type=int, default=None,
                       help="seed of the paired teams (default --seed, else 0)")

    p = sub.add_parser("offline", help="offline RL on human replays (human_data.py): bc or "
                                        "advantage-weighted bc")
    common(p)
    p.add_argument("--data", required=True, help="a human_data.py dataset (train/, val/, test/)")
    p.add_argument("--init", default=None, help="start from this checkpoint (else a new --size model "
                                                "reading the dataset's observation)")
    p.add_argument("--resume", default=None,
                   help="continue a stopped run after its last finished epoch, with its own model and "
                        "settings (its offline_last.pt; --ckpt-dir defaults to its folder)")
    p.add_argument("--ckpt-dir", default=None, help="default checkpoints/human-<algo>")
    p.add_argument("--size", default="wide-medium", choices=list(PRESETS))
    p.add_argument("--dropout", type=float, default=None)
    p.add_argument("--value-bins", type=int, default=None)
    p.add_argument("--move-tokens", action="store_true",
                   help="one token per (Pokémon, move slot); the move head reads our active's move "
                        "tokens after the transformer")
    p.add_argument("--typed-ffn", action="store_true",
                   help="one feed-forward block per token type in every layer")
    p.add_argument("--run", default=None, help="name under runs/ (train.log; default the ckpt dir's)")
    p.add_argument("--algo", default="bc", choices=["bc", "awbc"],
                   help="bc: imitate the human choices; awbc: weight them by w(A), A by GAE with the "
                        "value head")
    p.add_argument("--weight", default="exp", choices=["exp", "binary"],
                   help="awbc: min(exp(A / std / beta), --max-weight), or 1[A > 0]")
    p.add_argument("--beta", type=float, default=1.0)
    p.add_argument("--max-weight", type=float, default=20.0)
    p.add_argument("--warmup-epochs", type=int, default=1,
                   help="awbc: plain BC epochs first, while the critic learns")
    p.add_argument("--gae-lambda", type=float, default=0.95)
    p.add_argument("--vf-coef", type=float, default=0.5)
    p.add_argument("--value-ce-norm", default="log-bins", choices=["log-bins", "none"],
                   help="divide the two-hot value cross-entropy by log(bins) (~1 at init, like the "
                        "scalar head's MSE); none = the raw cross-entropy")
    p.add_argument("--epochs", type=int, default=10)
    p.add_argument("--minibatch", type=int, default=512)
    p.add_argument("--lr", type=float, default=3e-4, help="peak LR (cosine to --lr-min-ratio)")
    p.add_argument("--lr-min-ratio", type=float, default=0.1)
    p.add_argument("--warmup-steps", type=int, default=200)
    p.add_argument("--patience", type=int, default=2,
                   help="stop after this many epochs without a better validation NLL")
    p.add_argument("--min-rating", type=int, default=1700, help="the metrics' strong players")
    p.add_argument("--train-min-rating", type=int, default=0,
                   help="train the actor on the choices of players rated this or more only")
    p.set_defaults(fn=cmd_offline)

    p = sub.add_parser("offline-eval", help="top-1 / NLL of human choices and value AUC, "
                                             "calibration on a human_data.py split")
    common(p)
    p.add_argument("--ckpt", nargs="+", required=True,
                   help="checkpoints or released model folders (src/ElsaMina.Battles/Models/...)")
    p.add_argument("--data", required=True)
    p.add_argument("--split", default="test", choices=["train", "val", "test"])
    p.add_argument("--min-rating", type=int, default=1700)
    p.add_argument("--out", default=None, help="write the metrics to this JSON file")
    p.set_defaults(fn=cmd_offline_eval)

    p = sub.add_parser("eval", help="greedy win rate on paired teams (with a 95%% interval)")
    common(p)
    games(p)
    p.add_argument("--ckpt", required=True, help="a checkpoint or a released model folder")
    p.add_argument("--battles", type=int, default=1000)
    p.add_argument("--opponent", default="heuristic", choices=["heuristic", "random", "maxpower"])
    p.add_argument("--vs-ckpt", default=None,
                   help="play another checkpoint or released model folder instead (both greedy)")
    p.add_argument("--processes", type=int, default=1,
                   help="play the team pairs in this many processes (each one CPU core's worth "
                        "of poke-env and inference) and pool their results")
    p.add_argument("--search", action="store_true",
                   help="--ckpt searches one turn ahead on Showdown's simulator (search.py)")
    p.add_argument("--search-top-k", type=int, default=3, help="our candidate actions per search")
    p.add_argument("--search-min-prob", type=float, default=0.0,
                   help="...that the policy gives at least this probability (the first one always)")
    p.add_argument("--search-determinizations", type=int, default=2,
                   help="guesses of the opponent's sets per search")
    p.add_argument("--search-samples", type=int, default=1,
                   help="simulated turns per pair of actions and guess")
    p.add_argument("--search-workers", type=int, default=2, help="Node simulator processes")
    p.add_argument("--search-faint-reward", type=float, default=0.1,
                   help="the faint reward of the returns the model's value learned, added to each "
                        "simulated turn's value")
    p.add_argument("--search-depth", type=int, default=1, choices=[1, 2],
                   help="2: one more decision after the opponent's likeliest replies (search.py)")
    p.add_argument("--vs-search-depth", type=int, default=0, choices=[0, 1, 2],
                   help="--vs-ckpt searches too, this deep, with the other --search-* settings "
                        "(0: it plays its model alone), e.g. depth 2 vs depth 1")
    p.add_argument("--no-tera-before", type=int, default=0,
                   help="--ckpt never terastallizes before this turn (poke-env battle.turn); "
                        "with --vs-ckpt the same checkpoint, measures early tera")
    p.set_defaults(fn=cmd_eval)

    p = sub.add_parser("elo", help="round-robin between checkpoints and scripted players")
    common(p)
    games(p)
    p.add_argument("--ckpts", nargs="*", default=[])
    p.add_argument("--scripted", nargs="*", default=["heuristic", "maxpower", "random"],
                   choices=list(SCRIPTED))
    p.add_argument("--battles", type=int, default=200, help="battles per pair")
    p.add_argument("--anchors", default=None,
                   help="fixed ratings, e.g. random=0 (default: elo.ANCHORS, for the scripted "
                        "players taking part)")
    p.add_argument("--out", default=None, help="write the results and ratings to this JSON file")
    p.add_argument("--processes", type=int, default=1,
                   help="play this many matches at once, one process each")
    p.add_argument("--search-ckpts", nargs="*", default=[],
                   help="checkpoints that also play with search (search.py), as <name>+search")
    p.add_argument("--search-workers", type=int, default=2, help="Node simulator processes per search player")
    p.add_argument("--search-faint-reward", type=float, default=0.1,
                   help="the faint reward of the returns the searching models' value learned")
    p.set_defaults(fn=cmd_elo)
    return ap


def main() -> None:
    args = build_parser().parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
