"""
Record parity fixtures for ElsaMina's C# port of ``encoder.py``.

Two poke-env players play gen9randombattle against each other on a local
Showdown server. Both record, so every battle yields one fixture per side
(p1 and p2 perspectives). Each player keeps every raw protocol line of its
battles, appended *before* poke-env parses it. At every ``choose_move`` it
stores the number of lines received so far and ``encoder.encode(battle)``,
so ``lines[:line_count]`` is exactly what the battle had seen when it was
encoded.

One gzipped JSON file per battle side, written to ``--out``:

    {
      "format_version": 1,
      "battle_tag": "battle-gen9randombattle-12",
      "username": "...", "player_role": "p1", "poke_env_version": "0.16.1",
      "lines": ["|init|battle", "|title|...", ...],
      "error_lines": [],          # indices of lines poke-env raised on (rest of frame dropped)
      "decisions": [
        {
          "line_count": 42,
          "mon_ids": [...], "move_ids": [...], "mon_feats": [...],
          "move_feats": [...], "field": [...], "mask": [...],   # flat, row-major
          "team_idents": ["p1: Garchomp", ...],                # battle.team keys
          "orders": {"6": "/choose move earthquake", ...},     # legal action -> poke-env order
          "logits": [...], "value": 0.1                         # minipoke.onnx output, when --model is set
        }
      ]
    }

Usage (with a Showdown server on localhost:8000), for model version N:

    python make_fixtures.py --version N --battles 20

reads the model and spec exported to src/ElsaMina.Battles/Models/MiniPokeVN and
writes the fixtures to test/ElsaMina.UnitTests/Battles/Strategies/MiniPokeVN/Fixtures.
--model, --spec and --out override those paths. Run it with the encoder.py the
model was trained with: the fixtures record what that encoder.py produces.

Actions are picked randomly among the legal ones, favoring terastallization
when it is available so that tera turns show up in most battles, and
otherwise following SimpleHeuristicsPlayer half of the time so that battles
look like real games (setup, hazards, status, forced switches).
"""

from __future__ import annotations

import argparse
import asyncio
import gzip
import json
import random
from importlib.metadata import version
from pathlib import Path
from typing import Dict, List, Optional

import numpy as np
from poke_env import AccountConfiguration
from poke_env.environment import SinglesEnv
from poke_env.player import Player, SimpleHeuristicsPlayer

import encoder as E

FORMAT = "gen9randombattle"
REPO_ROOT = Path(__file__).resolve().parents[2]
ARRAYS = ["mon_ids", "move_ids", "mon_feats", "move_feats", "field", "mask"]


class RecordingPlayer(Player):
    def __init__(self, rng: random.Random, tera_bias: float, session=None, **kwargs):
        super().__init__(**kwargs)
        self.rng = rng
        self.tera_bias = tera_bias
        self.session = session
        self.lines: Dict[str, List[str]] = {}
        self.decisions: Dict[str, List[dict]] = {}
        self.errors: Dict[str, List[int]] = {}

    async def _handle_battle_message(self, split_messages: List[List[str]]):
        # Feed poke-env one line at a time so each line is recorded right before
        # it is parsed, and choose_move sees exactly the recorded prefix.
        header = split_messages[0]
        tag = header[0][1:]
        for split_message in split_messages[1:]:
            lines = self.lines.setdefault(tag, [])
            lines.append("|".join(split_message))
            try:
                await super()._handle_battle_message([header, split_message])
            except Exception:
                # poke-env drops the rest of the frame when a line raises: keep that
                # behavior, but flag the line so the parity test can explain diffs.
                self.errors.setdefault(tag, []).append(len(lines) - 1)
                raise

    def choose_move(self, battle):
        obs = E.encode(battle)
        tag = battle.battle_tag
        record = {"line_count": len(self.lines[tag])}
        for name in ARRAYS:
            record[name] = obs[name].ravel().tolist()
        record["team_idents"] = list(battle.team.keys())
        record["orders"] = self._legal_orders(obs["mask"], battle)
        if self.session is not None:
            logits, value = self.session.run(None, {n: obs[n][None] for n in ARRAYS})
            record["logits"] = logits[0].tolist()
            record["value"] = float(value[0])
        self.decisions.setdefault(tag, []).append(record)

        if not obs["mask"].any():
            return self.choose_random_move(battle)
        return E.action_to_order(self._pick(obs["mask"], battle), battle)

    @staticmethod
    def _legal_orders(mask: np.ndarray, battle) -> Dict[str, Optional[str]]:
        orders = {}
        for action in np.flatnonzero(mask):
            try:
                order = SinglesEnv.action_to_order(
                    np.int64(E.compact_to_env_action(int(action))), battle, strict=True
                )
                orders[str(int(action))] = order.message
            except ValueError:
                orders[str(int(action))] = None
        return orders

    def _pick(self, mask: np.ndarray, battle) -> int:
        legal = [int(a) for a in np.flatnonzero(mask)]
        tera = [a for a in legal if a >= 10]
        if tera and self.rng.random() < self.tera_bias:
            return self.rng.choice(tera)
        if self.rng.random() < 0.5:
            order, _ = SimpleHeuristicsPlayer.choose_singles_move(battle)
            action = E.order_to_compact_action(order, battle)
            if action is not None and mask[action]:
                return action
        return self.rng.choice(legal)


def write_fixtures(player: RecordingPlayer, out: Path) -> int:
    count = 0
    for tag, battle in player.battles.items():
        decisions = player.decisions.get(tag, [])
        if not decisions:
            continue
        fixture = {
            "format_version": 1,
            "battle_tag": tag,
            "username": player.username,
            "player_role": battle.player_role,
            "poke_env_version": version("poke-env"),
            "lines": player.lines[tag],
            "error_lines": player.errors.get(tag, []),
            "decisions": decisions,
        }
        path = out / f"{tag}-{battle.player_role}.json.gz"
        with gzip.open(path, "wt", encoding="utf-8") as f:
            json.dump(fixture, f, separators=(",", ":"))
        count += len(decisions)
    return count


def check_spec(spec_path: Optional[str]) -> None:
    if spec_path is None:
        return
    spec = json.loads(Path(spec_path).read_text())
    assert spec["vocab"]["species"] == E.SPECIES_VOCAB, "species vocab differs from encoder.py"
    assert spec["vocab"]["move"] == E.MOVE_VOCAB, "move vocab differs from encoder.py"
    assert spec["vocab"]["ability"] == E.ABILITY_VOCAB, "ability vocab differs from encoder.py"
    assert (spec["f_mon"], spec["f_move"], spec["f_field"]) == (E.F_MON, E.F_MOVE, E.F_FIELD)


async def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--version", type=int, help="model version N: use the MiniPokeVN model, spec and fixtures paths")
    ap.add_argument("--battles", type=int, default=20)
    ap.add_argument("--out", help="fixtures directory")
    ap.add_argument("--model", help="minipoke.onnx: also record the model's logits")
    ap.add_argument("--spec", help="encoder_spec.json: check it matches encoder.py")
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--tera-bias", type=float, default=0.3)
    args = ap.parse_args()

    if args.version is not None:
        name = f"MiniPokeV{args.version}"
        model_dir = REPO_ROOT / "src" / "ElsaMina.Battles" / "Models" / name
        args.model = args.model or str(model_dir / "minipoke.onnx")
        args.spec = args.spec or str(model_dir / "encoder_spec.json")
        args.out = args.out or str(
            REPO_ROOT / "test" / "ElsaMina.UnitTests" / "Battles" / "Strategies" / name / "Fixtures")
    if args.out is None:
        ap.error("--out (or --version) is required")

    check_spec(args.spec)
    session = None
    if args.model:
        import onnxruntime as ort

        session = ort.InferenceSession(args.model)

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    rng = random.Random(args.seed)
    suffix = rng.randrange(10_000)
    players = [
        RecordingPlayer(
            rng=random.Random(rng.random()),
            tera_bias=args.tera_bias,
            session=session,
            account_configuration=AccountConfiguration(f"Fixture{name}{suffix}", None),
            battle_format=FORMAT,
            max_concurrent_battles=1,
        )
        for name in ("Alpha", "Beta")
    ]
    await players[0].battle_against(players[1], n_battles=args.battles)

    total = sum(write_fixtures(p, out) for p in players)
    print(f"wrote {total} decisions from {args.battles} battles to {out}")


if __name__ == "__main__":
    asyncio.run(main())
