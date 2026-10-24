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


def _row_selection_losses(out, labels):
    """aux_losses as first written: on the labelled rows only (variable shapes)."""
    import torch.nn.functional as F
    res, set_loss = {}, []
    ok = labels["aux_action"] >= 0
    logits = out["action"][ok].masked_fill(~labels["aux_action_mask"][ok], -1e9)
    res["aux_action_loss"] = F.cross_entropy(logits, labels["aux_action"][ok])
    res["aux_action_acc"] = (logits.argmax(-1) == labels["aux_action"][ok]).float().mean()
    for name in ("item", "ability", "tera"):
        target = labels[f"aux_{name}"]
        logits = out[name][target >= 0]
        set_loss.append(F.cross_entropy(logits, target[target >= 0]))
        res[f"aux_{name}_acc"] = (logits.argmax(-1) == target[target >= 0]).float().mean()
    ok = (labels["aux_moves"] > 0).any(-1)
    logits, ids = out["moves"][ok], labels["aux_moves"][ok]
    target = torch.zeros_like(logits).scatter_(1, ids, (ids > 0).float())
    target[:, 0] = 0.0
    n_true = target.sum(-1).clamp_min(1.0)
    set_loss.append(-(target / n_true[:, None] * torch.log_softmax(logits, -1)).sum(-1).mean())
    top = logits.topk(ids.shape[1], dim=-1).indices
    res["aux_moves_recall"] = (target.gather(1, top).sum(-1) / n_true).mean()
    res["aux_set_loss"] = torch.stack(set_loss).sum()
    return res


def _random_labels(b, seed, share):
    """Labels for a batch of `b`, each row / Pokémon labelled with probability `share`."""
    g = torch.Generator().manual_seed(seed)
    labelled = lambda *shape: torch.rand(*shape, generator=g) < share
    vocab = lambda name, *shape: torch.randint(1, E.VOCAB_SIZES[name], shape, generator=g)
    action = torch.randint(0, 14, (b,), generator=g)
    mask = (torch.rand(b, 14, generator=g) < 0.5) | (torch.arange(14) == action[:, None])
    moves = vocab("move", b, 6, 4) * (torch.rand(b, 6, 4, generator=g) < 0.8)
    moves[..., 0] = vocab("move", b, 6)                       # at least one known move
    return {"aux_action": torch.where(labelled(b), action, NO_LABEL),
            "aux_action_mask": mask,
            "aux_item": torch.where(labelled(b, 6), vocab("item", b, 6), NO_LABEL),
            "aux_ability": torch.where(labelled(b, 6), vocab("ability", b, 6), NO_LABEL),
            "aux_tera": torch.where(labelled(b, 6), torch.randint(0, E.NT, (b, 6), generator=g),
                                    NO_LABEL),
            "aux_moves": moves * labelled(b, 6)[..., None]}


def _aux_out(b, seed):
    g = torch.Generator().manual_seed(seed)
    shapes = {"action": (b, 14), "item": (b, 6, E.VOCAB_SIZES["item"]),
              "ability": (b, 6, E.VOCAB_SIZES["ability"]),
              "moves": (b, 6, E.VOCAB_SIZES["move"]), "tera": (b, 6, E.NT)}
    return {k: torch.randn(*s, generator=g).requires_grad_() for k, s in shapes.items()}


def test_aux_losses_match_the_labelled_rows_and_their_gradients():
    out, labels = _aux_out(32, seed=1), _random_labels(32, seed=2, share=0.4)
    res, ref = aux_losses(out, labels), _row_selection_losses(out, labels)
    assert res.keys() == ref.keys()
    for k in ref:
        assert res[k].item() == pytest.approx(ref[k].item(), rel=1e-5, abs=1e-6), k
    loss = res["aux_action_loss"] + res["aux_set_loss"]
    grads = torch.autograd.grad(loss, list(out.values()))
    ref_grads = torch.autograd.grad(ref["aux_action_loss"] + ref["aux_set_loss"], list(out.values()))
    for g, r in zip(grads, ref_grads):
        assert torch.allclose(g, r, atol=1e-6)


def test_aux_losses_shapes_do_not_depend_on_the_labels():
    """The MPS leak: every op must see the same shapes whatever is labelled."""
    from torch.overrides import TorchFunctionMode

    class Shapes(TorchFunctionMode):
        def __init__(self):
            super().__init__()
            self.seen = []

        def __torch_function__(self, func, types, args=(), kwargs=None):
            result = func(*args, **(kwargs or {}))
            for t in result if isinstance(result, (tuple, list)) else [result]:
                if isinstance(t, torch.Tensor):
                    self.seen.append(tuple(t.shape))
            return result

    def shapes(labels):
        with Shapes() as mode:
            aux_losses(_aux_out(32, seed=1), labels)
        return mode.seen

    assert shapes(_random_labels(32, seed=2, share=0.3)) == \
        shapes(_random_labels(32, seed=3, share=0.7))


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
