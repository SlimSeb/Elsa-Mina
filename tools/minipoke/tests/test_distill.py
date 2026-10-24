"""C4: a V1 teacher plays from its own encoding, the dataset stores the v2 student's."""

import asyncio

import numpy as np
import pytest

from dataset import ShardDataset, ShardWriter
from encoder_v2 import EncoderV1, EncoderV2
from players import DistillPlayer
from tests.battles import default_team, feed, make_battle, opponent_switch
from tests.priors import hand_priors


class FakeTeacher:
    """An InferenceBatcher stand-in: a V1 model that always plays Earthquake."""

    def __init__(self):
        self.encoder = EncoderV1()
        self.seen = []

    async def act(self, obs, greedy=False):
        self.seen.append(obs)
        probs = np.zeros(14, dtype=np.float32)
        probs[6] = 0.75
        probs[7] = 0.25
        return 6, float(np.log(0.75)), 0.4, probs


def test_cross_encoder_distill_stores_the_students_encoding(tmp_path):
    teacher = FakeTeacher()
    student = EncoderV2(hand_priors(), history=True)
    writer = ShardWriter(tmp_path, shard_size=8, obs_layout=student.layout)
    player = DistillPlayer(teacher, writer, gamma=0.9, epsilon=0.0, encoder=student,
                           battle_format="gen9randombattle", start_listening=False)
    battle = make_battle(default_team(), [opponent_switch("Venusaur"), "|turn|1",
                                          "|move|p2a: Venusaur|Earth Power|p1a: Garchomp",
                                          "|turn|2"])
    order = asyncio.run(player._choose(battle))
    assert order.message == "/choose move earthquake"
    expected = student.encode(battle)
    feed(battle, "|move|p1a: Garchomp|Earthquake|p2a: Venusaur", "|turn|3")
    asyncio.run(player._choose(battle))
    battle._won = True
    player._on_finished(battle)
    writer.close()

    # The teacher was fed V1 observations only
    assert [list(o) for o in teacher.seen] == [EncoderV1().input_names] * 2
    # The dataset holds the student's v2 layout and encoding
    data = ShardDataset(tmp_path, val_frac=0.0)
    assert data.obs_layout == student.layout
    rows = next(data.batches("train", 10))
    obs = {k: v[0] for k, v in rows["obs"].items()}
    assert list(obs) == student.input_names
    for k, v in expected.items():
        np.testing.assert_allclose(obs[k], v, atol=1e-3, err_msg=k)
    np.testing.assert_allclose(rows["probs"][0], [0, 0, 0, 0, 0, 0, 0.75, 0.25, 0, 0, 0, 0, 0, 0],
                               atol=1e-3)
    assert list(rows["action"]) == [6, 6] and rows["value"][0] == pytest.approx(0.4, abs=1e-3)
    assert rows["ret"][1] == pytest.approx(1.0, abs=1e-3)   # won on the last decision


def test_same_encoder_distill_records_the_teachers_observation(tmp_path):
    teacher = FakeTeacher()
    writer = ShardWriter(tmp_path, shard_size=8)
    player = DistillPlayer(teacher, writer, gamma=0.9, epsilon=0.0,
                           battle_format="gen9randombattle", start_listening=False)
    battle = make_battle(default_team(), [opponent_switch("Venusaur"), "|turn|1"])
    asyncio.run(player._choose(battle))
    assert player.encoder is teacher.encoder
    battle._won = False
    player._on_finished(battle)
    writer.close()
    assert ShardDataset(tmp_path).obs_layout == EncoderV1().layout
