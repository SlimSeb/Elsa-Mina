"""D1: auxiliary heads, their labels from the other seat, their losses."""

import numpy as np
import pytest
import torch

import encoder as E
from aux_labels import AuxLabeler, NO_LABEL, empty_labels
from encoder_v2 import TYPE_INDEX, item_hash
from model import with_aux_heads
from ppo import AUX_STATS, PPOConfig, aux_losses, ppo_update
from tests.battles import feed, make_battle, mon, opponent_switch
from tests.helpers import make_model, random_episodes


def their_team():
    team = [mon("Venusaur", ["Giga Drain", "Sludge Bomb", "Sleep Powder", "Leech Seed"], level=84,
                item="blacksludge", ability="chlorophyll", tera="Steel", role="p2"),
            mon("Iron Valiant", ["Moonblast", "Close Combat", "Knock Off", "Encore"], level=79,
                item="boosterenergy", ability="quarkdrive", tera="Fairy", role="p2"),
            mon("Garchomp", ["Earthquake", "Outrage", "Fire Fang", "Stone Edge"], level=77,
                item="choiceband", ability="roughskin", tera="Ground", role="p2")]
    team[0]["active"] = True
    return team


def our_team():
    team = [mon("Corviknight", ["Brave Bird", "Roost", "Defog", "U-turn"], level=81)]
    team[0]["active"] = True
    return team


def seats(lines_p1, lines_p2):
    p1 = make_battle(our_team(), lines_p1)
    p2 = make_battle(their_team(), lines_p2)
    return p1, p2


START = ["|turn|1"]


def test_labels_from_the_other_seat():
    labeler = AuxLabeler()
    # turn 1 from p1's view: Venusaur came in, nothing revealed yet
    p1, p2 = seats([opponent_switch("Venusaur"), *START],
                   [opponent_switch("Corviknight", 81, role="p1"), *START])
    # p2 switches to Iron Valiant (its slot 1, not revealed to p1): our slot 1, the next one
    p2_mask = E.action_mask(p2)
    labeler.decision(p2, 1, p2_mask)
    ctx = labeler.decision(p1, 6, E.action_mask(p1))
    labels = labeler.labels(ctx)
    assert int(labels["aux_action"]) == 1
    legal = set(np.flatnonzero(labels["aux_action_mask"]))
    assert legal == {1, 6, 10}  # switches to the unrevealed Valiant / Garchomp -> slot 1;
    #                             any of Venusaur's 4 unrevealed moves -> its move 0 (+ tera)
    # Sets of the revealed Venusaur, from p2's own request
    assert labels["aux_item"][0] == item_hash("blacksludge")
    assert labels["aux_ability"][0] == E.ABILITY_VOCAB["chlorophyll"]
    assert labels["aux_tera"][0] == TYPE_INDEX["steel"]
    assert sorted(labels["aux_moves"][0]) == sorted(
        E.MOVE_VOCAB[m] for m in ("gigadrain", "sludgebomb", "sleeppowder", "leechseed"))
    assert labels["aux_item"][1] == NO_LABEL     # nothing else revealed to p1


def test_move_labels_use_our_view_of_the_revealed_moves():
    labeler = AuxLabeler()
    p1, p2 = seats([opponent_switch("Venusaur"), "|turn|1",
                    "|move|p2a: Venusaur|Sleep Powder|p1a: Corviknight", "|turn|2"],
                   [opponent_switch("Corviknight", 81, role="p1"), "|turn|1",
                    "|move|p2a: Venusaur|Sleep Powder|p1a: Corviknight", "|turn|2"])
    # p2's own move order: Giga Drain, Sludge Bomb, Sleep Powder, Leech Seed
    labeler.decision(p2, 6 + 2, E.action_mask(p2))       # Sleep Powder: p1 saw it first
    ctx = labeler.decision(p1, 6, E.action_mask(p1))
    assert int(labeler.labels(ctx)["aux_action"]) == 6 + 0
    labeler.clear()
    labeler.decision(p2, 10 + 1, E.action_mask(p2))      # tera + Sludge Bomb, not seen yet
    ctx = labeler.decision(p1, 6, E.action_mask(p1))
    assert int(labeler.labels(ctx)["aux_action"]) == 10 + 1
    # no decision of theirs in that turn -> no label
    ctx = labeler.decision(p1, 6, E.action_mask(p1))
    labeler.decisions.clear()
    assert int(labeler.labels(ctx)["aux_action"]) == NO_LABEL


def test_aux_losses():
    torch.manual_seed(0)
    model = make_model("small", aux_heads=True).eval()
    from tests.test_model import batch_for
    obs = batch_for(model, n=4)
    with torch.no_grad():
        out = model.forward_train(obs, aux=True)[3]
    assert out["action"].shape == (4, 14) and out["item"].shape == (4, 6, E.VOCAB_SIZES["item"])
    assert out["moves"].shape == (4, 6, E.VOCAB_SIZES["move"]) and out["tera"].shape == (4, 6, E.NT)
    labels = {k: torch.from_numpy(np.stack([v] * 4)) for k, v in empty_labels().items()}
    assert aux_losses(out, labels) == {}                              # nothing labelled
    labels["aux_action"][1] = 3
    labels["aux_action_mask"][1] = torch.tensor([i in (3, 7) for i in range(14)])
    labels["aux_item"][2, 0] = 5
    labels["aux_moves"][0, 1] = torch.tensor([10, 20, 30, 0])
    res = aux_losses(out, labels)
    two = out["action"][1, [3, 7]]
    assert res["aux_action_loss"].item() == pytest.approx(
        -torch.log_softmax(two, -1)[0].item(), rel=1e-5)
    assert res["aux_item_acc"].item() in (0.0, 1.0)
    logq = torch.log_softmax(out["moves"][0, 1], -1)
    item_ce = -torch.log_softmax(out["item"][2, 0], -1)[5]
    assert res["aux_set_loss"].item() == pytest.approx(
        (item_ce - logq[[10, 20, 30]].mean()).item(), rel=1e-5)
    assert 0.0 <= res["aux_moves_recall"].item() <= 1.0


def test_ppo_update_trains_the_aux_heads():
    model = make_model("small", aux_heads=True)
    episodes = random_episodes(4, 20, seed=3)
    for ep in episodes:
        for t, step in enumerate(ep):
            labels = empty_labels()
            labels["aux_action"] = np.array(t % 14)
            labels["aux_tera"][0] = 4
            step["aux"] = labels
    opt = torch.optim.AdamW(model.parameters(), lr=3e-3)
    cfg = PPOConfig(minibatch=40, epochs=2, aux_action_coef=1.0, aux_set_coef=1.0, target_kl=0.0)
    first = ppo_update(model, opt, episodes, cfg, "cpu")
    for _ in range(5):
        last = ppo_update(model, opt, episodes, cfg, "cpu")
    assert set(AUX_STATS) <= set(last)
    assert last["aux_set_loss"] < first["aux_set_loss"] and last["aux_tera_acc"] == 1.0
    assert np.isnan(last["aux_item_acc"])                    # no item labels


def test_aux_heads_are_added_without_touching_the_rest_and_never_exported(tmp_path):
    import onnx
    from export_onnx import export, parity
    base = make_model("small")
    model = with_aux_heads(base)
    for k, v in base.state_dict().items():
        assert torch.equal(model.state_dict()[k], v)
    assert any(k.startswith("aux_") for k in model.state_dict())
    onnx_path = export(model.eval(), tmp_path)
    graph = onnx.load(str(onnx_path)).graph
    assert not any("aux" in init.name for init in graph.initializer)
    assert parity(model, onnx_path)["logits"] < 1e-4
