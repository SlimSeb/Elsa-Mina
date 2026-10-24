"""C3: history features from poke-env's state and the protocol log."""

import numpy as np
import pytest

import encoder as E
from encoder_v2 import EncoderV2, F_HIST
from history import last_chosen_moves, previous_turn, switched_by_choice
from model import MiniPokeTransformer, ModelConfig
from tests.battles import default_team, feed, make_battle, opponent_switch
from tests.priors import hand_priors


@pytest.fixture(scope="module")
def enc():
    return EncoderV2(hand_priors(), history=True)


def split(*lines):
    return [line.split("|") for line in lines]


def test_previous_turn_and_switch_by_choice():
    lines = split(opponent_switch("Venusaur"), "|turn|1",
                  "|switch|p2a: Garchomp|Garchomp, L77|100/100",      # by choice
                  "|move|p1a: Garchomp|Earthquake|p2a: Garchomp",
                  "|turn|2",
                  "|move|p2a: Garchomp|U-turn|p1a: Garchomp",         # a move, then out
                  "|switch|p2a: Venusaur|Venusaur, L84|100/100",
                  "|turn|3")
    assert previous_turn(lines[:4]) == []                                # turn 1 only
    turn1 = previous_turn(lines[:6])
    assert switched_by_choice(turn1, "p2") and not switched_by_choice(turn1, "p1")
    assert not switched_by_choice(previous_turn(lines), "p2")
    # a replacement after a faint is not a choice either
    faint = split("|move|p1a: Garchomp|Earthquake|p2a: Venusaur", "|faint|p2a: Venusaur",
                  "|switch|p2a: Garchomp|Garchomp, L77|100/100")
    assert not switched_by_choice(faint, "p2")


def test_locked_and_called_moves_are_not_choices():
    lines = split(opponent_switch("Garchomp", 77), "|turn|1",
                  "|move|p2a: Garchomp|Outrage|p1a: Garchomp", "|turn|2",
                  "|move|p2a: Garchomp|Outrage|p1a: Garchomp|[from]lockedmove", "|turn|3",
                  "|move|p2a: Garchomp|Sleep Talk|p2a: Garchomp",
                  "|move|p2a: Garchomp|Earthquake|p1a: Garchomp|[from]move: Sleep Talk")
    assert last_chosen_moves(lines, "p2", 3) == ["Sleep Talk", "Outrage"]
    # moves before the Pokémon's last entrance do not count
    lines += split("|switch|p2a: Venusaur|Venusaur, L84|100/100")
    assert last_chosen_moves(lines, "p2") == []


def test_history_features(enc):
    battle = make_battle(default_team(), [
        opponent_switch("Garchomp", 77), "|turn|1",
        "|move|p2a: Garchomp|Earthquake|p1a: Garchomp", "|turn|2",
        "|move|p1a: Garchomp|Stealth Rock|p2a: Garchomp",
        "|move|p2a: Garchomp|Earthquake|p1a: Garchomp", "|turn|3"])
    obs = enc.encode(battle)
    assert list(obs)[-2:] == ["hist_ids", "hist_feats"]
    assert obs["hist_feats"].shape == (F_HIST,)
    assert list(obs["hist_ids"]) == [E.MOVE_VOCAB["stealthrock"], E.MOVE_VOCAB["earthquake"]]
    f = obs["hist_feats"]
    assert f[0] == pytest.approx(0.3)      # garchomp came in before turn 1: 3 |turn| lines
    assert f[2] == 0.0 and f[3] == 0.0     # nobody switched in turn 2
    assert f[6] == 1.0                     # Earthquake twice in a row
    assert f[7] == 1.0                     # and Garchomp's sets can be all attacks

    feed(battle, "|switch|p2a: Venusaur|Venusaur, L84|100/100",
         "|move|p1a: Garchomp|Earthquake|p2a: Venusaur", "|turn|4")
    f = enc.encode(battle)["hist_feats"]
    assert f[0] == pytest.approx(0.1) and f[2] == 1.0
    assert f[6] == 0.0 and f[7] == 0.0
    assert enc.encode(battle)["hist_ids"][1] == E.PAD_ID   # Venusaur has not moved yet


def test_protect_counter_and_no_choice_lock_with_status_moves(enc):
    battle = make_battle(default_team(), [
        opponent_switch("Venusaur"), "|turn|1",
        "|move|p2a: Venusaur|Sleep Powder|p1a: Garchomp", "|turn|2",
        "|move|p2a: Venusaur|Sleep Powder|p1a: Garchomp", "|turn|3",
        "|move|p1a: Garchomp|Protect|p1a: Garchomp", "|-singleturn|p1a: Garchomp|Protect",
        "|turn|4",
        "|move|p1a: Garchomp|Protect|p1a: Garchomp", "|-singleturn|p1a: Garchomp|Protect",
        "|turn|5"])
    f = enc.encode(battle)["hist_feats"]
    assert f[5] == pytest.approx(2 / 3)      # two protects in a row
    assert f[6] == 1.0 and f[7] == 0.0       # Sleep Powder twice: a status move, no Choice item


def test_history_needs_encoder_v2_and_feeds_the_field_token():
    with pytest.raises(ValueError):
        MiniPokeTransformer(ModelConfig(history=True))
    from tests.helpers import make_model
    from tests.test_model import batch_for
    import torch
    model = make_model("small", encoder_version=2, history=True).eval()
    obs = batch_for(model)
    other = {k: v.clone() for k, v in obs.items()}
    other["hist_feats"][:, 6:] = 1.0 - other["hist_feats"][:, 6:]
    with torch.no_grad():
        assert not torch.allclose(model(obs)[1], model(other)[1])


# --------------------------------------------------------------------------
# D3: history event tokens
# --------------------------------------------------------------------------
EVENT_LINES = [
    "|switch|p1a: Garchomp|Garchomp, L77|250/250", opponent_switch("Venusaur"), "|turn|1",
    "|move|p1a: Garchomp|Earthquake|p2a: Venusaur", "|-resisted|p2a: Venusaur",
    "|-damage|p2a: Venusaur|70/100",
    "|move|p2a: Venusaur|Sleep Powder|p1a: Garchomp|[miss]", "|-miss|p2a: Venusaur|p1a: Garchomp",
    "|turn|2",
    "|switch|p2a: Iron Valiant|Iron Valiant, L79|100/100",
    "|move|p1a: Garchomp|Outrage|p2a: Iron Valiant", "|-immune|p2a: Iron Valiant", "|turn|3",
    "|move|p2a: Iron Valiant|Moonblast|p1a: Garchomp", "|-supereffective|p1a: Garchomp",
    "|-crit|p1a: Garchomp", "|-damage|p1a: Garchomp|0 fnt", "|faint|p1a: Garchomp",
]


def test_recent_events():
    from history import recent_events
    events = recent_events(split(*EVENT_LINES), 16)
    assert [(e.side, e.species, e.move, e.turn) for e in events] == [
        ("p1", "garchomp", None, 0), ("p2", "venusaur", None, 0),
        ("p1", "garchomp", "earthquake", 1), ("p2", "venusaur", "sleeppowder", 1),
        ("p2", "ironvaliant", None, 2), ("p1", "garchomp", "outrage", 2),
        ("p2", "ironvaliant", "moonblast", 3)]
    assert events[2].flags == {"resisted": True}
    assert events[3].flags == {"miss": True}
    assert events[5].flags == {"immune": True}
    assert events[6].flags == {"supereffective": True, "crit": True, "ko": True}
    assert [e.move for e in recent_events(split(*EVENT_LINES), 2)] == ["outrage", "moonblast"]
    # a hazard KO on entry is not credited to a move of an earlier turn
    hazard = split(*EVENT_LINES[:12], "|switch|p2a: Venusaur|Venusaur, L84|70/100",
                   "|-damage|p2a: Venusaur|0 fnt|[from] Stealth Rock", "|faint|p2a: Venusaur")
    assert not any(e.flags.get("ko") for e in recent_events(hazard, 16))
    called = split("|move|p2a: Venusaur|Sleep Talk|p2a: Venusaur",
                   "|move|p2a: Venusaur|Giga Drain|p1a: Garchomp|[from]move: Sleep Talk")
    assert [e.called for e in recent_events(called, 4)] == [False, True]


def test_event_arrays_replace_the_history_features():
    enc = EncoderV2(hand_priors(), history=True, history_tokens=6)
    assert "hist_ids" not in enc.layout and enc.layout["event_ids"][0] == (6, 2)
    battle = make_battle(default_team(), EVENT_LINES[1:-2])     # up to the crit, Garchomp alive
    obs = enc.encode(battle)
    ids, feats = obs["event_ids"], obs["event_feats"]
    assert feats[:, 0].tolist() == [1.0] * 6                  # 7 events, the last 6 kept
    assert ids[-1].tolist() == [E.MOVE_VOCAB["moonblast"], E.SPECIES_VOCAB["ironvaliant"]]
    assert ids[3].tolist() == [E.PAD_ID, E.SPECIES_VOCAB["ironvaliant"]]   # an entrance
    assert feats[3, 3] == 1.0 and feats[3, 2] == 0.0
    assert feats[-1, 1] == 0.0 and feats[-2, 1] == 1.0        # theirs, ours
    assert feats[-1, 6] == 1.0 and feats[-1, 8] == 1.0        # crit, super effective
    assert feats[0, 13] == pytest.approx(0.3)                 # Venusaur came in before turn 1
    assert feats[1, 13] == pytest.approx(0.2)                 # Earthquake: turn 1, now turn 3
    short = make_battle(default_team(), [opponent_switch("Venusaur"), "|turn|1"])
    feats = enc.encode(short)["event_feats"]
    assert feats[:, 0].tolist() == [0.0] * 4 + [1.0] * 2     # padding first


def test_history_tokens_model():
    import torch
    from tests.helpers import make_model
    from tests.test_model import batch_for
    with pytest.raises(ValueError):
        MiniPokeTransformer(ModelConfig(history_tokens=8))
    model = make_model("small", encoder_version=2, history=True, history_tokens=8).eval()
    assert not model.hist_features and model.n_tokens == 15 + 8
    assert [t for t, _, _ in model.segments][-1] == "events"
    obs = batch_for(model)
    obs["event_feats"][0, :3, 0] = 0.0
    x, present, _ = model.embed(obs)
    assert present.shape == (6, 23) and not present[0, 15:18].any()
    other = {k: v.clone() for k, v in obs.items()}
    other["event_ids"][:, -1, 0] = (other["event_ids"][:, -1, 0] + 5) % 900 + 2
    with torch.no_grad():
        assert not torch.allclose(model(obs)[1], model(other)[1])
