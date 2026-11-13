"""
Human replays -> training shards for offline RL (train.py offline), from their re-simulations.

    python human_data.py replays/human/gen9randombattle replays/human/gen9randombattle-first-step-zipped \
        --encoder-from ../../src/ElsaMina.Battles/Models/MiniPokeV2_1_22M --out data/human-v21

Per battle (resim.py): re-simulated on its own Showdown version, and kept only when it replays
exactly (its omniscient view equals the replay's log). Battles forfeited (or timed out) before
turn --min-turns are dropped. Then each side whose player is rated --min-player-rating or more
(its own rating, on its `>player` line) is fed to poke-env 0.16.1 as its websocket received it
(resim.side_decisions), every decision point is encoded with the student's encoder
(--encoder-from: a checkpoint, or a released model's folder: its flags and its set priors), and
the input log choice that answered it is mapped to the 14 actions (analyze_replays.choice_to_action:
moves by name, `switch N` = the request's N-th Pokémon, terastallize = +4). A choice that maps to
no action legal in poke-env's mask keeps its step, unlabelled (action -1): the trajectory stays
whole for the returns and advantages.

Rewards are the ones the models are trained with (players.Trajectories): after each decision,
--faint-reward x (their Pokémon fainted - ours) until the next one, and the result on the last
(+1 win, -1 loss, 0 tie). `ret` is the discounted return (--gamma).

Battles are split by upload time, whole: the most recent --test-frac are the test set, the
--val-frac before them the validation set (early stopping), the rest trains.

    <out>/train/, <out>/val/, <out>/test/   dataset.ShardDataset each (read them with val_frac=0),
        observation + probs (one-hot of the human's action; zeros unlabelled), action (-1:
        unlabelled), ret, value (0), and the extra fields traj (battle index x 2 + side), step,
        reward, done (last decision of its trajectory), rating, turn, label_ok, result (the
        side's result: 1 win, -1 loss, 0 tie)
    <out>/meta.json   counts, filters, splits and their dates, gamma, faint reward, the encoder's
        flags (`model_flags`) and set priors (`set_priors`): a new model trains with these
    <out>/battles.jsonl   one line per battle: id, version, upload time, split, status, sides kept
    <out>/chunks/   each finished chunk's result: a build that stops (Ctrl-C, a reboot) resumes
        where it stopped when run again with the same command line
"""

from __future__ import annotations

import argparse
import hashlib
import json
import logging
import multiprocessing
import shutil
import time
from collections import Counter
from concurrent.futures import ProcessPoolExecutor, as_completed
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Sequence

import numpy as np

from dataset import ShardWriter, discounted_returns, write_meta
from resim import (SIDES, ResimWorker, answered_choices, as_choice, judge, read_replay,
                   replay_files, side_decisions)
from showdown_versions import BuildFailed, DEFAULT_ROOT, ShowdownVersions, short

log = logging.getLogger("minipoke")
FORMAT = "gen9randombattle"
SPLITS = ("train", "val", "test")
EXTRA = {"traj": ((), "int32"), "step": ((), "int16"), "reward": ((), "float16"),
         "done": ((), "uint8"), "rating": ((), "int16"), "turn": ((), "int16"),
         "label_ok": ((), "uint8"), "result": ((), "int8")}
# Rows per shard: a writer preallocates them (V2.1: 11.7 KB a row, 47 MB a shard). Not much more:
# a buffer of 1M rows is 11 GB, and once freed memory is reused it is all resident
SHARD_ROWS = 4096
MODEL_FLAGS = ("encoder_version", "cand_moves", "history", "history_tokens", "matchup_tokens",
               "move_effects")


# --------------------------------------------------------------------------
# The student's encoder
# --------------------------------------------------------------------------
def load_encoder(source: Optional[str]):
    """(encoder, model flags, set priors dict or None) of --encoder-from: a .pt checkpoint, a
    released model's folder (its encoder_spec.json and set_priors.json), or None for V1."""
    from encoder_v2 import EncoderV1, encoder_for
    from model import ModelConfig
    if source is None:
        return EncoderV1(), {"encoder_version": 1}, None
    path = Path(source)
    if path.is_dir() or path.name == "encoder_spec.json":
        from make_fixtures import encoder_from_spec
        spec_path = path / "encoder_spec.json" if path.is_dir() else path
        spec = json.loads(spec_path.read_text())
        encoder = encoder_from_spec(str(spec_path))
        flags = {k: v for k, v in (spec.get("model") or {"encoder_version": 1}).items() if k in MODEL_FLAGS}
    else:
        from checkpoint import load_checkpoint
        from setpriors import SetPriors
        state = load_checkpoint(path)
        cfg = ModelConfig.from_dict(state["cfg"])
        priors = SetPriors.from_dict(state["set_priors"]) if state.get("set_priors") else None
        encoder = encoder_for(cfg, priors)
        flags = {k: getattr(cfg, k) for k in MODEL_FLAGS}
    priors = getattr(encoder, "priors", None)
    return encoder, flags, (priors.to_dict() if priors is not None else None)


# --------------------------------------------------------------------------
# One battle
# --------------------------------------------------------------------------
def side_rewards(fainted: Sequence[Sequence[int]], final: Sequence[int], result: float,
                 faint_reward: float) -> List[float]:
    """Each decision's reward (players.Trajectories): the faint difference until the next
    decision, plus the result on the last one. `fainted[t]` = (ours, theirs) at decision t."""
    n = len(fainted)
    out = []
    for t in range(n):
        after = fainted[t + 1] if t + 1 < n else final
        reward = faint_reward * ((after[1] - fainted[t][1]) - (after[0] - fainted[t][0]))
        out.append(reward + (result if t == n - 1 else 0.0))
    return out


def outcome(omniscient: Sequence[str], names: Dict[str, str]) -> Dict[str, float]:
    """Each side's result (+1, -1, 0 for a tie) from the battle's |win| / |tie| line."""
    from analyze_replays import to_id
    for line in reversed(omniscient):
        if line.startswith("|win|"):
            winner = to_id(line[5:])
            won = [s for s in SIDES if to_id(names.get(s)) == winner]
            if won:
                return {s: (1.0 if s == won[0] else -1.0) for s in SIDES}
            break
        if line == "|tie":
            return {s: 0.0 for s in SIDES}
    return {s: 0.0 for s in SIDES}


def last_turn(lines: Sequence[str]) -> int:
    for line in reversed(lines):
        if line.startswith("|turn|"):
            return int(line[6:])
    return 0


def encode_side(res, side: str, encoder, result: float, faint_reward: float, gamma: float):
    """(rows, stats) of one side of a kept re-simulation: rows = (obs, action, label_ok, reward,
    ret, done, turn) per decision point, in order."""
    import encoder as E
    from analyze_replays import choice_to_action
    stats = Counter()
    dec = side_decisions(res.sides[side], res.replay.name(side), {"s": encoder}, res.replay.id)
    decisions = dec["decisions"]
    choices, strays = answered_choices(decisions, res.requests[side])
    if strays or dec["parse_errors"]:
        stats["side_poke_env_errors"] += 1
        return [], stats
    if not decisions:
        return [], stats
    rewards = side_rewards([d["fainted"] for d in decisions], dec["final_fainted"], result, faint_reward)
    rets = discounted_returns(rewards, gamma)
    rows = []
    for t, d in enumerate(decisions):
        obs = d["obs"]["s"]
        choice = as_choice(side, choices[t])
        action, problem = (choice_to_action(choice, d["team"], d["request_team"], d["moves"])
                           if choice is not None else (None, "unanswered"))
        ok = action is not None and bool(obs["mask"][action])
        if not ok:
            stats["unlabelled_" + ("illegal in poke-env's mask" if action is not None
                                   else (problem or "unmapped"))] += 1
        stats["decisions"] += 1
        stats["labelled"] += int(ok)
        rows.append((obs, action if ok else -1, ok, rewards[t], float(rets[t]),
                     t == len(decisions) - 1, d["turn"]))
    assert E.ACT_DIM == len(obs["mask"])
    return rows, stats


# --------------------------------------------------------------------------
# Worker processes: a chunk of battles each, into their own shard folders
# --------------------------------------------------------------------------
_W: dict = {}


def _init_worker(encoder_from: Optional[str], versions_root: str, settings: dict) -> None:
    logging.basicConfig(level=logging.WARNING)
    import torch
    torch.set_num_threads(1)
    encoder, _, _ = load_encoder(encoder_from)
    _W.update(encoder=encoder, versions=ShowdownVersions(versions_root), workers={}, **settings)


def _worker(version: str) -> ResimWorker:
    """This process's Node worker for `version`; the one of another version is closed (a chunk is
    one version: keeping one Node process per version seen would hold them all)."""
    for other in [v for v in _W["workers"] if v != version]:
        _W["workers"].pop(other).close()
    if version not in _W["workers"]:
        _W["workers"][version] = ResimWorker(_W["versions"].path(version))
    return _W["workers"][version]


def process_chunk(task: dict) -> dict:
    """Re-simulates, checks and encodes a chunk of battles of one version; writes its samples
    to <out>/<split>/c<chunk>/. Returns the shards written, stats and per-battle records."""
    out, chunk = Path(task["out"]), task["chunk"]
    encoder = _W["encoder"]
    writers: Dict[str, ShardWriter] = {}
    stats: Counter = Counter()
    records = []
    for item in task["battles"]:
        replay = read_replay(Path(item["path"]))
        record = {"id": replay.id, "version": short(replay.version), "upload_time": replay.upload_time,
                  "split": item["split"], "sides": []}
        try:
            res = judge(replay, _worker(replay.version).resim(replay.id, replay.inputlog))
        except (RuntimeError, ValueError, OSError) as exc:
            _W["workers"].pop(replay.version, None)
            record["status"] = f"error: {exc}"[:200]
            stats["battle_error"] += 1
            records.append(record)
            continue
        record["status"] = res.status
        if not res.ok:
            stats[f"battle_{res.status}"] += 1
            records.append(record)
            continue
        turns = last_turn(res.omniscient)
        record["turns"] = turns
        if ">forcelose" in replay.inputlog and turns < _W["min_turns"]:
            record["status"] = "early_forfeit"
            stats["battle_early_forfeit"] += 1
            records.append(record)
            continue
        results = outcome(res.omniscient, {s: replay.name(s) for s in SIDES})
        stats["battle_kept"] += 1
        for k, side in enumerate(SIDES):
            rating = replay.rating(side)
            if rating is None or rating < _W["min_player_rating"]:
                stats["side_low_rating"] += 1
                continue
            rows, side_stats = encode_side(res, side, encoder, results[side], _W["faint_reward"],
                                           _W["gamma"])
            stats.update(side_stats)
            if not rows:
                continue
            stats["side_kept"] += 1
            record["sides"].append(side)
            split = item["split"]
            if split not in writers:
                # A writer preallocates shard_size rows (~11.7 KB each with V2.1's observation)
                writers[split] = ShardWriter(out / split / f"c{chunk:05d}", shard_size=SHARD_ROWS,
                                             obs_layout=encoder.layout, extra=EXTRA)
            traj = item["index"] * 2 + k
            for step, (obs, action, ok, reward, ret, done, turn) in enumerate(rows):
                probs = np.zeros(len(obs["mask"]), dtype=np.float32)
                if ok:
                    probs[action] = 1.0
                writers[split].add(obs, probs, action, 0.0, ret, traj=traj, step=step, reward=reward,
                                   done=int(done), rating=rating, turn=turn, label_ok=int(ok),
                                   result=int(results[side]))
        records.append(record)
    shards = {}
    for split, writer in writers.items():
        shards[split] = [{"file": f"c{chunk:05d}/{s['file']}", "n": s["n"]} for s in writer.close()]
    return {"shards": shards, "stats": dict(stats), "records": records}


# --------------------------------------------------------------------------
def assign_splits(times: Sequence[int], val_frac: float, test_frac: float) -> List[str]:
    """Split names by upload time: the newest `test_frac` test, the `val_frac` before val."""
    order = np.argsort(np.asarray(times), kind="stable")
    n = len(times)
    n_test, n_val = int(round(n * test_frac)), int(round(n * val_frac))
    out = ["train"] * n
    for rank, i in enumerate(order):
        if rank >= n - n_test:
            out[i] = "test"
        elif rank >= n - n_test - n_val:
            out[i] = "val"
    return out


def utc(ts: int) -> str:
    return datetime.fromtimestamp(ts, timezone.utc).strftime("%Y-%m-%d %H:%M")


def resume_chunks(out: Path, tasks: List[dict], settings: dict) -> Dict[int, dict]:
    """The results of the chunks a stopped build of the same plan finished (<out>/chunks/), so a
    rerun of the same command line only does the rest. A chunk that was being written when the
    build stopped is removed. A different plan (other replays or settings) refuses to mix."""
    plan = {"settings": settings, "tasks": [[t["chunk"], [b["path"] for b in t["battles"]]] for t in tasks]}
    digest = hashlib.sha256(json.dumps(plan, sort_keys=True).encode()).hexdigest()
    plan_path = out / "chunks" / "plan.json"
    if plan_path.exists():
        if json.loads(plan_path.read_text()).get("sha256") != digest:
            raise SystemExit(f"{out} holds a stopped build of other replays or settings: "
                             f"remove it, or pick another --out")
    else:
        if out.exists() and any(out.iterdir()):
            raise SystemExit(f"{out} is not empty and holds no build plan: pick another --out")
        plan_path.parent.mkdir(parents=True, exist_ok=True)
        plan_path.write_text(json.dumps({"sha256": digest, "chunks": len(tasks)}))
    results = {}
    for task in tasks:
        path = out / "chunks" / f"c{task['chunk']:05d}.json"
        if path.exists():
            results[task["chunk"]] = json.loads(path.read_text())
        else:
            for split in SPLITS:
                shutil.rmtree(out / split / f"c{task['chunk']:05d}", ignore_errors=True)
    return results


def save_chunk(out: Path, chunk: int, result: dict) -> None:
    path = out / "chunks" / f"c{chunk:05d}.json"
    tmp = path.with_suffix(".tmp")
    tmp.write_text(json.dumps(result))
    tmp.replace(path)


def build(args) -> dict:
    out = Path(args.out)
    if (out / "meta.json").exists():
        raise SystemExit(f"{out} already holds a dataset: pick another --out")
    paths, dupes = replay_files(args.replays)
    if args.limit:
        paths = paths[: args.limit]
    replays = []
    skipped = Counter()
    for path in paths:
        try:
            replay = read_replay(path)
        except (OSError, ValueError, KeyError):
            skipped["unreadable"] += 1
            continue
        fmt = json.loads(replay.inputlog.split("\n")[1][len(">start "):]).get("formatid") \
            if replay.inputlog.startswith(">version") and "\n>start " in replay.inputlog else None
        if fmt != FORMAT:
            skipped["other format or no input log"] += 1
            continue
        if not any((replay.rating(s) or 0) >= args.min_player_rating for s in SIDES):
            skipped["no side rated enough"] += 1
            continue
        replay.log, replay.inputlog = [], ""   # the workers read the file again: keep RAM flat
        replays.append(replay)
    replays.sort(key=lambda r: (r.upload_time, r.id))
    splits = assign_splits([r.upload_time for r in replays], args.val_frac, args.test_frac)
    log.info("%d replays (%d duplicate ids, skipped: %s)", len(replays), dupes, dict(skipped))

    encoder, flags, priors = load_encoder(args.encoder_from)
    versions = ShowdownVersions(args.versions)
    build_failed = {}
    for version in sorted({r.version for r in replays}):
        try:
            versions.ensure(version)
        except BuildFailed as exc:
            build_failed[short(version)] = str(exc)[:200]
    # Chunks of one version each, in time order
    tasks, by_version = [], {}
    for i, (replay, split) in enumerate(zip(replays, splits)):
        if short(replay.version) in build_failed or not replay.version:
            continue
        by_version.setdefault(replay.version, []).append({"path": str(replay.path), "index": i, "split": split})
    for version, items in by_version.items():
        for start in range(0, len(items), args.chunk):
            tasks.append({"out": str(out), "chunk": len(tasks), "battles": items[start:start + args.chunk]})
    settings = {"min_player_rating": args.min_player_rating, "min_turns": args.min_turns,
                "faint_reward": args.faint_reward, "gamma": args.gamma}
    t0 = time.time()
    results = resume_chunks(out, tasks, {**settings, "encoder_from": args.encoder_from,
                                         "val_frac": args.val_frac, "test_frac": args.test_frac})
    todo = [t for t in tasks if t["chunk"] not in results]
    if results:
        log.info("resuming: %d chunks done, %d to go", len(results), len(todo))
    # Each worker process is replaced after 25 chunks: whatever poke-env or the allocator keeps
    # goes with it
    with ProcessPoolExecutor(args.processes, mp_context=multiprocessing.get_context("spawn"),
                             initializer=_init_worker, max_tasks_per_child=25,
                             initargs=(args.encoder_from, str(args.versions), settings)) as pool:
        futures = {pool.submit(process_chunk, task): task["chunk"] for task in todo}
        for done, future in enumerate(as_completed(futures), 1):
            chunk = futures[future]
            results[chunk] = future.result()
            save_chunk(out, chunk, results[chunk])
            if done % 10 == 0 or done == len(todo):
                log.info("%d / %d chunks, %d samples (%.0f s)", len(results), len(tasks),
                         sum(s["n"] for r in results.values() for v in r["shards"].values() for s in v),
                         time.time() - t0)
    stats: Counter = Counter({f"battle_build_failed": sum(
        1 for r in replays if short(r.version) in build_failed or not r.version)})
    shards = {s: [] for s in SPLITS}
    records = []
    for chunk in sorted(results):
        result = results[chunk]
        stats.update(result["stats"])
        records += result["records"]
        for split, files in result["shards"].items():
            shards[split] += files
    dates = {}
    for split in SPLITS:
        times = [r.upload_time for r, s in zip(replays, splits) if s == split]
        dates[split] = [utc(min(times)), utc(max(times))] if times else None
    info = {"source": "human replays", "replays": [str(p) for p in args.replays],
            "encoder_from": args.encoder_from, "model_flags": flags, "set_priors": priors,
            "min_player_rating": args.min_player_rating, "min_turns": args.min_turns,
            "gamma": args.gamma, "faint_reward": args.faint_reward,
            "val_frac": args.val_frac, "test_frac": args.test_frac, "dates": dates,
            "build_failed": build_failed, "stats": dict(stats)}
    for split in SPLITS:
        write_meta(out / split, sorted(shards[split], key=lambda s: s["file"]),
                   {**info, "split": split}, encoder.layout, EXTRA)
    samples = {split: sum(s["n"] for s in shards[split]) for split in SPLITS}
    (out / "meta.json").write_text(json.dumps({**info, "samples": samples}, indent=1))
    with open(out / "battles.jsonl", "w") as f:
        for record in sorted(records, key=lambda r: (r["upload_time"], r["id"])):
            f.write(json.dumps(record) + "\n")
    return {**info, "samples": samples, "seconds": time.time() - t0}


def describe(summary: dict) -> str:
    s = summary["stats"]
    lines = [f"samples: {summary['samples']}  ({summary.get('seconds', 0):.0f} s)",
             "battles: " + ", ".join(f"{k[7:]} {v}" for k, v in sorted(s.items()) if k.startswith("battle_")),
             "sides: " + ", ".join(f"{k[5:]} {v}" for k, v in sorted(s.items()) if k.startswith("side_")),
             f"decisions: {s.get('decisions', 0)}, labelled {s.get('labelled', 0)}"
             + "".join(f"; {k[11:]} {v}" for k, v in sorted(s.items()) if k.startswith("unlabelled_")),
             "splits by upload time: " + ", ".join(f"{k} {v[0]} .. {v[1]}" for k, v in summary["dates"].items() if v)]
    if summary.get("build_failed"):
        lines.append(f"versions that failed to build: {summary['build_failed']}")
    return "\n".join(lines)


def main(argv: Optional[List[str]] = None) -> None:
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s", datefmt="%H:%M:%S")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("replays", nargs="+", type=Path, help="folders (any depth) or files of replays")
    ap.add_argument("--out", required=True, help="the dataset folder (must not hold one)")
    ap.add_argument("--encoder-from", default=None,
                    help="the student's encoder: a checkpoint, or a released model's folder "
                         "(src/ElsaMina.Battles/Models/...: its spec and set priors); default V1")
    ap.add_argument("--min-player-rating", type=int, default=1500,
                    help="keep the sides of players rated this or more (their own rating)")
    ap.add_argument("--min-turns", type=int, default=3,
                    help="drop battles forfeited or timed out before this turn")
    ap.add_argument("--val-frac", type=float, default=0.05, help="validation battles (by upload time)")
    ap.add_argument("--test-frac", type=float, default=0.1, help="test battles: the most recent")
    ap.add_argument("--gamma", type=float, default=0.995)
    ap.add_argument("--faint-reward", type=float, default=0.1,
                    help="reward per Pokémon fainted (theirs - ours) after each decision, as in training")
    ap.add_argument("--processes", type=int, default=3,
                    help="worker processes (each ~0.5 GB, plus a ~0.2 GB Node process)")
    ap.add_argument("--chunk", type=int, default=100, help="battles per task (and shard folder)")
    ap.add_argument("--versions", type=Path, default=DEFAULT_ROOT, help="the Showdown checkouts folder")
    ap.add_argument("--limit", type=int, default=None, help="only the first N replays found")
    args = ap.parse_args(argv)
    print(describe(build(args)))


if __name__ == "__main__":
    main()
