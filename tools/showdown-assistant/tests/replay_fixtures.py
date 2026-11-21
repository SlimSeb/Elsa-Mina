r"""
Replays the parity fixtures (battles recorded by tools/minipoke/make_fixtures.py) through the assistant server's HTTP
API and compares its advice with what Python recorded: the probabilities, and the recommended action (it can only
differ where the guard vetoes the model's choice). Start the server first, then, from the repository root:

    python3 tools/showdown-assistant/tests/replay_fixtures.py http://127.0.0.1:7685 \
        test/ElsaMina.UnitTests/Battles/Strategies/MiniPokeV3_22M/Fixtures/*.json.gz
"""
import gzip
import json
import math
import sys
import urllib.request

BASE = sys.argv[1]
FIXTURES = sys.argv[2:]


def post(path, body):
    request = urllib.request.Request(BASE + path, data=json.dumps(body).encode(), method="POST",
                                     headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read())


def softmax(logits, mask):
    legal = [l for l, m in zip(logits, mask) if m]
    top = max(legal)
    exps = [math.exp(l - top) if m else 0.0 for l, m in zip(logits, mask)]
    total = sum(exps)
    return [e / total for e in exps]


print(json.loads(urllib.request.urlopen(BASE + "/status").read()))
checked = mismatched_best = max_diff = 0.0
vetoed = 0
for path in FIXTURES:
    fixture = json.load(gzip.open(path))
    room = fixture["battle_tag"]
    lines = fixture["lines"]
    applied = 0
    first = True
    for decision in fixture["decisions"]:
        frame = ">" + room + "\n" + "\n".join(lines[applied:decision["line_count"]])
        applied = decision["line_count"]
        advice = post(f"/battles/{room}/frames", {"frames": [frame], "reset": first})
        first = False
        if advice["status"] != "advice":
            print(room, "decision at", applied, "->", advice["status"], advice.get("message"))
            continue
        expected = softmax(decision["logits"], decision["mask"])
        ours = {a["action"]: a["probability"] for a in advice["actions"]}
        for action, probability in ours.items():
            max_diff = max(max_diff, abs(probability - expected[action]))
        recorded_best = max(range(14), key=lambda i: decision["logits"][i] if decision["mask"][i] else -1e18)
        checked += 1
        if advice["best"] != recorded_best:
            best_row = next(a for a in advice["actions"] if a["action"] == recorded_best)
            if best_row["veto"]:
                vetoed += 1
            else:
                mismatched_best += 1
                print(room, "turn", advice["turn"], "best", advice["best"], "recorded", recorded_best)
    # The rest of the battle, then the room closes
    rest = post(f"/battles/{room}/frames", {"frames": [">" + room + "\n" + "\n".join(lines[applied:])]})
    closed = post(f"/battles/{room}/frames", {"frames": [">" + room + "\n|deinit"]})
    print(room, "end:", rest["status"], "then", closed["status"])

print(f"decisions {int(checked)}, best differs {int(mismatched_best)} (+{vetoed} guard vetoes), "
      f"max probability diff {max_diff:.2e}")
example = advice
print(json.dumps({k: v for k, v in example.items() if k != "actions"}, indent=1))
for action in example["actions"]:
    print(" ", action)
