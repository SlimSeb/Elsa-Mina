# Gladion: battle models for ElsaMina

Small transformers that play gen 9 random battles in ElsaMina (`-strategy gladionv1-22m+search`, see
[Using a model in ElsaMina](#using-a-model-in-elsamina)). They learn from human ladder games: a public
Showdown replay holds the battle's input log, so re-simulating it rebuilds what each player saw, and the
model is trained offline on the choices of the players rated 1500+, by behavioural cloning (BC) or
advantage-weighted BC (AWBC), as Metamon did (Grigsby et al., RLC 2025,
[arXiv:2504.04395](https://arxiv.org/abs/2504.04395): offline RL on human replays beat imitating them).
At decision time, ElsaMina can also search one turn ahead on Showdown's simulator.

`GladionV1_22M` (AWBC on 258k human battles, ~22M parameters) plays at 1800+ on the gen9randombattle
ladder with search (Nov 2026). The models before it, trained by self-play PPO up to `MiniPokeV2_1_22M`,
stayed around 1300, and PPO fine-tuning made the human-data model worse (see
[Results](#results)), so the self-play training code and those models were removed in Nov 2026: git
history has them.

**Gladion was called minipoke** until Nov 23, 2026 (`tools/minipoke`, `MiniPoke*` classes, `minipoke.onnx`).
Gladion v1 is the model released as minipoke v3 (`MiniPokeV3_22M`, strategy `minipokev3-22m`): same
weights, same observation. The minipoke models before it never became Gladion models and keep their
names in [Results](#results) and [History](#history). Folders of the runs made before the rename keep the
old numbering (`checkpoints/v3/`, `runs/v3/`: Gladion v1's runs).

## The pipeline

| Step | Script | Output |
|---|---|---|
| 1. Download public replays rated 1500+, with their input logs | `collect_replays.py` | `replays/human/<format>/<UTC day>/<id>.json.gz` |
| 2. Re-simulate each on its own server version, check it against the replay | `resim.py check` | `showdown-versions/` (one checkout per version) |
| 3. Encode each player's states, choices and returns | `human_data.py` | `data/<name>/{train,val,test}` |
| 4. Train | `train.py offline --algo bc\|awbc` | `checkpoints/<run>/offline.pt` |
| 5. Measure on the human test battles, then in games | `train.py offline-eval`, `train.py eval`, `train.py elo` | |
| 6. Release into ElsaMina | `export_onnx.py`, `make_fixtures.py` | `src/ElsaMina.Battles/Models/GladionVN_SIZE/` |
| 7. Analyze the bot's own ladder games | `analyze_replays.py` | `replays/analysis/report.md` |

`replays/`, `data/`, `checkpoints/`, `runs/`, `export/`, `showdown-versions/` and the Showdown clone
stay in this directory, ignored by git. The Mac has 16 GB: keep data jobs to about 3 processes.
[docs/ubuntu.md](docs/ubuntu.md) runs steps 1 to 5 on the replay server and the GPU box.

## Setup (macOS, Apple Silicon)

```bash
cd tools/gladion

# 1. Python env
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt          # torch from PyPI includes MPS on macOS
pip install -r requirements-dev.txt      # + pytest, matplotlib, psutil

# 2. One local Showdown server (current Showdown needs Node 22+), for eval, elo, the search,
#    the parity fixtures and the random battle sets the set priors read
git clone https://github.com/smogon/pokemon-showdown.git
cd pokemon-showdown && npm install && cp config/config-example.js config/config.js
node pokemon-showdown start --no-security   # leave running, port 8000
```

`eval` and `elo` start the server themselves with `--showdown-cmd "node pokemon-showdown start
--no-security"`, and look for the clone in `--showdown-cwd` (default `pokemon-showdown`). With many
battles at once, raise `exports.subprocesses` in Showdown's `config/config.js`. Re-simulation does not
use this clone: it builds one checkout per server version.

poke-env is pinned (0.16.1) on purpose: ElsaMina's battle tracker is a port of that exact version, and a
model only plays well on the state it was trained on.

## Collecting human replays

Every public replay's JSON (`https://replay.pokemonshowdown.com/<id>.json`) holds the battle's input
log: the server version, the battle and team seeds, both players' ratings and every choice.
`collect_replays.py` downloads them, rated 1500+ by default:

```bash
caffeinate -is python collect_replays.py                       # runs until stopped, resumes where it stopped
python collect_replays.py --min-rating 1600 --oldest-days 90
python collect_replays.py --skip-from replays/human/old-batch --skip-from old-batch-2.zip
python collect_replays.py --summary                            # replays, sides by rating, server versions
```

It only knows the replays in its own folder. To archive some (move or zip day folders), keep
`state.json` where it is and pass each archive with `--skip-from` (a folder at any depth, or a .zip), or
the replays at the edges of the moved ones are downloaded again.

To skip a span collected on another machine, give its edges (UTC dates, or unix times):
`--backfill-from` starts the backfill there (replays uploaded before it) and `--head-from` the new
uploads (from it on). They never move a cursor back over what the folder already covered, so the same
command line restarts the run. Nov 2026:
- The Mac's two folders hold every 1500+ replay from 2026-10-30 13:08:14 to 2026-11-12 21:59:58 UTC.
  The server skipped that span (`--backfill-from "2026-10-30 13:08:14" --head-from "2026-11-12 21:59:58"`).
- The server's folders, copied to the GPU box, hold every 1500+ replay from 2026-02-14 11:34:23 to
  2026-11-19 17:48:21 UTC, except the Mac's span: `gen9randombattle` (104,225 replays, Jul 30 to Nov 15)
  and `gen9randombattle-new` (156,747 replays, collected with `--skip-from` the first folder). They are
  the 258k battles `GladionV1_22M` trained on. The Mac's replays were never added to them.
- A new round goes on from those edges, into a new folder (a dataset build in progress refuses a
  folder whose files change):
  `--head-from "2026-11-19 17:48:21" --backfill-from "2026-02-14 11:34:23"`, with one `--skip-from` per
  folder already collected.

It checks for new uploads every 15 minutes and otherwise pages back through older ones (up to
`--oldest-days`, 365). It sends one request at a time, about 2 s apart (`--delay`), with its own
User-Agent (Cloudflare refuses Python's default one). It waits on 429 and 5xx (Retry-After, or 1 minute
doubling), slows down after a 429, and stops after 4 refusals (403) in a row: wait a few hours, then run
it again with a longer `--delay`. At 2 s it saves about 1,500 replays an hour while paging back (Nov
2026: 75 to 280 public gen9randombattle replays are uploaded per hour, about a quarter of them rated
1500+). The listed rating is the replay's, close to the players' average: the dataset filters on each
player's own rating (its `>player` line).

## Re-simulation

Run through its server version's simulator, a replay's input log rebuilds what each player's client
received (a refused choice never reaches the input log).

- `showdown_versions.py` keeps one checkout per version under `showdown-versions/`: a blobless bare
  clone of smogon/pokemon-showdown, a worktree per commit, `node build`, and one `node_modules` per
  distinct `package-lock.json` (`npm ci --omit=dev`), shared through a symlink. A version builds in
  ~40 s the first time, ~4 s after; failures are recorded in `failed.json` and not retried unless asked.
- `resim.js` (Node, one process per version) writes the input log line by line into that version's own
  `BattleStream` (`_writeLine`, then `sendUpdates`, as the server writes every line on its own), splits
  each update per player with the version's own `extractChannelMessages`, and adds the `rqid` the server
  adds to requests (`room-battle.ts`: one counter per battle, both sides, first request 2). It records
  which input log choice answered each request. `>chat` lines are skipped.
- `resim.py` drives the workers and judges each re-simulation: every choice accepted, the battle ended,
  and its omniscient view (exact HP of both sides: that is what a public log is) equal to the replay's
  log, line for line, once both are reduced to the simulator's lines (`battle_lines`: joins, chat,
  timers, `|raw|` and the like are left out).

```bash
python resim.py check replays/human/gen9randombattle replays/human/gen9randombattle-first-step-zipped
python resim.py parity replays/replays          # the bot's own games against their .logs
```

Nov 2026, the Mac's 9,368 human replays: **all 9,358 distinct ones replay exactly**, on 6 server
versions, in 90 s with 6 workers. The server's first 104,225 replays (Jul 30 to Nov 15): **all replay
exactly** too, on 29 server versions, in 635 s with 8 workers on the GPU box.

**Parity.** The bot's own ladder games (`-savereplays` keeps the replay with its input log, and the
`.log` of every line ElsaMina received live) check the whole chain: the bot's side of the
re-simulation must be the `.log`'s battle lines exactly (requests and their rqid included), poke-env fed
either one must make the same decisions with the same encodings, and the re-simulation's choices must
map to the same actions as `analyze_replays.py`'s alignment of the `.log`. Of 561 games: **558
identical** (12,271 decision points). The other 3 are games where Showdown refused the bot's switch
while trapped: their `.log` has the `|error|` and a second request, which the input log cannot hold.
`tests/test_resim.py` runs the same check on three of them (`tests/fixtures/resim`, names anonymized).

A re-simulated side needs two things the room sends: poke-env counts a battle at `|init|battle` (and
waits for that count at `|win|`), so `resim.side_decisions` prepends it; and it reads the format from
the room name, so the tag is `battle-<replay id>`.

## The dataset

```bash
python human_data.py replays/human/gen9randombattle replays/human/gen9randombattle-first-step-zipped \
    --encoder-from ../../src/ElsaMina.Battles/Models/GladionV1_22M --out data/human-v21
```

Each kept battle's sides whose player is rated `--min-player-rating` (1500) or more, by its own rating,
are fed to poke-env 0.16.1 (`analyze_replays.ReplayPlayer`), encoded with the student's encoder
(`--encoder-from`: a checkpoint, or a released model's folder: its flags and set priors), and each
choice is mapped to the 14 actions (`analyze_replays.choice_to_action`). Battles forfeited or timed out
before turn 3 are dropped. Rewards: `--faint-reward` 0.1 per faint difference after each decision, +-1
at the end; returns discounted by `--gamma` 0.995. The value head learns these returns, and the search
scores its leaves with the same faint reward. A decision without a usable label (an unanswered last
request, a choice outside poke-env's mask) keeps its step, with action -1, so trajectories stay whole.

Shards keep, besides the observation and the one-hot action, the fields offline RL needs to put rows
back in trajectory order (`dataset.py` extra fields): `traj` (battle x 2 + side), `step`, `reward`,
`done`, `rating`, `turn`, `label_ok`, `result`, and the targets of the opponent heads
(`opp_targets.py`, -1 = none; see [The observation and the model](#the-observation-and-the-model)):

| Field | Shape, dtype | |
|---|---|---|
| `opp_act` | int16 | the opponent's choice from the same state, one of 12 + K classes in our view's terms |
| `opp_tera` | int8 | it terastallized (a move choice, when it could) |
| `opp_item` | int16 [6] | per opposing slot of our view: its real item, hashed like `encoder.item_id` |
| `opp_ability` | int16 [6] | its real base ability (`ABILITY_VOCAB`) |
| `opp_tera_type` | int8 [6] | its real tera type (an index into `encoder.TYPES`) |
| `opp_moves` | int16 [6, 4] | its real moves (`MOVE_VOCAB`, 0 = none) |

Datasets built before them (Nov 22, 2026) have none: they still train everything else, and need
rebuilding with `human_data.py` to train the opponent heads. Battles are split whole by upload time: the newest
`--test-frac` 10% test, the `--val-frac` 5% before them validation (early stopping), the rest trains.
Each chunk of 100 battles is a task (`--processes`, default 3: each ~0.6 GB plus a ~0.2 GB Node
process). Workers are replaced every 25 chunks by starting a new pool for each round of processes x 25
chunks: Python's `max_tasks_per_child` hangs once every worker has retired (CPython gh-115634). A stopped
build resumes when run again with the same command line (`<out>/chunks/`), as long as the replay
folders don't change. Shards are float16 features and int16 ids: about 170 bytes a decision.

Nov 2026:

| Dataset | Replays | Battles kept | Decisions | Train / val / test states | Splits (UTC) |
|---|---|---|---|---|---|
| The Mac's | 9,368 | 9,259 | 554,196 | | |
| `data/human-v21` (GPU box) | 104,225 | 103,281 | 6.21M | 5.29M / 314k / 611k | train Jul 30 to Oct 16, val to Oct 20, test to Nov 15 |
| `data/human-v21b` (GPU box) | 260,972 | 258,474 | 15.40M | 13.05M / 793k / 1.56M | train Feb 14 to Sep 28, val to Oct 9, test to Nov 19 |

The Mac's: 18,412 sides (106 under 1500), 548,042 decisions labelled (the rest are last requests nobody
answered), 93 MB, built in ~6 min with 3 processes. `human-v21` takes 1.1 GB. On the GPU box a build
does about 18 chunks a minute with 4 processes. `human-v21b` (both of the server's folders) trained
`GladionV1_22M`.

## Offline training

```bash
python train.py offline --data data/human-v21 --size wide-medium --algo bc   --ckpt-dir checkpoints/v3/bc-4m
python train.py offline --data data/human-v21 --size wide-medium --algo awbc --ckpt-dir checkpoints/v3/awbc-4m
python train.py offline --resume checkpoints/v3/awbc-4m/offline_last.pt --data data/human-v21   # after a stop
```

Both algorithms train the value head on the dataset's returns (`--vf-coef` 0.5 x the value loss: MSE
for a scalar head, the two-hot cross-entropy divided by log(bins) for a distributional one, so a fresh
head scores 1 either way). Steps without a label train the value head, never the actor.
- `bc`: -log pi(a|s) of the human's action.
- `awbc` (AWR / CRR): -w(A) log pi(a|s). A is GAE (gamma 0.995, lambda `--gae-lambda` 0.95) along each
  human trajectory with the value head as the critic, recomputed by an ordered pass over the training
  set at the start of each epoch; `--weight exp`: min(exp(A / std(A) / `--beta`), `--max-weight` 20),
  `--weight binary`: 1[A > 0]. The first `--warmup-epochs` (1) are plain BC while the critic learns.

A model with opponent heads also trains them (`--opp-coef` x their loss, see
[The observation and the model](#the-observation-and-the-model)).

The labels are one-hot, so a big model memorizes them: every epoch is scored on the validation battles,
the best one (validation NLL) is `offline.pt` (`offline_last.pt` is the last, with what `--resume`
needs), and training stops after `--patience` (2) epochs without a better one. At the end, the best
epoch is measured on the test battles (`offline_metrics.json`).

The log prints one line per epoch and nothing in between, so a long epoch can look like a hang. On the
GPU box `large` trains at about 12 minutes per million training states: 48 min a BC epoch on
`human-v21`, about 65 min an AWBC epoch (its ordered pass comes on top), 2 h 40 min an AWBC epoch on
`human-v21b`. The first AWBC epoch is shorter, because it is plain BC. To stop a run, press Ctrl-C about
30 s after an `epoch N/M` line, once its checkpoints are saved. `--resume` then continues exactly: the
optimizer, the LR schedule, early stopping and the shuffling. On a finished run it only recomputes the
test metrics.

| Option | Default | |
|---|---|---|
| `--size` | `wide-medium` | a new model's architecture (below); it reads the dataset's observation (encoder flags, set priors) |
| `--init CKPT` | | start from a checkpoint instead (same observation as the dataset) |
| `--epochs`, `--patience` | 10, 2 | |
| `--lr`, `--lr-min-ratio`, `--warmup-steps` | 3e-4, 0.1, 200 | peak LR, cosine to that fraction of it |
| `--minibatch` | 512 | |
| `--dropout` | 0 | an option against memorization on a big new model |
| `--train-min-rating` | 0 | train the actor only on players rated this or more (the dataset already starts at 1500) |
| `--min-rating` | 1700 | the metrics' strong players |
| `--move-tokens`, `--typed-ffn` | off | architecture options (see [The observation and the model](#the-observation-and-the-model)) |
| `--move-pool` | `concat` | how a new model's Pokémon tokens read their known moves: `concat` (Gladion v1's) or `meanmax` (below); with `--init`, only the checkpoint's own |
| `--opp-heads` | off | training-only opponent heads (below), on a new model or added to `--init`'s; the dataset needs their targets |
| `--opp-coef` | 0.5 | x the opponent heads' loss, for a model that has them (0: not trained) |
| `--run` | the checkpoint folder's name | `runs/<run>/train.log` |

### Sizes

| `--size` | d_model / layers / heads | FFN | Value head | Parameters (V2.1 observation) |
|---|---|---|---|---|
| `small` | 128 / 3 / 4 | x2 | scalar | 0.98M |
| `medium` | 256 / 4 / 8 | x2 | 51-bin two-hot over [-1.6, 1.6] | 4.1M |
| `wide` | 256 / 3 / 8 | x4 | scalar | 4.4M |
| `wide-medium` | 256 / 3 / 8 | x4 | 51-bin | 4.4M |
| `large` | 512 / 5 / 8 | x4 | 51-bin | 22.5M (`GladionV1_22M`) |

`large` is sized for an 8 GB GPU (the Radeon RX 6600 XT box): training at `--minibatch` 512 takes
about 3.2 GiB with the desktop. What limits growth is git, not memory: models are committed, GitHub
warns above 50 MiB and refuses 100 MiB. Measured shapes (ONNX size, and what one decision costs
ElsaMina on one CPU thread):

| d_model / layers | Parameters | ONNX | CPU per decision |
|---|---|---|---|
| 256 / 3 (`wide-medium`) | 4.4M | 17 MiB | 1.4 ms |
| 384 / 3 | 9.4M | 36 MiB | 3.2 ms |
| 512 / 3 | 16.2M | 62 MiB | 5.3 ms |
| 512 / 4 | 19.3M | 74 MiB | 6.3 ms |
| **512 / 5 (`large`)** | 22.5M | 86 MiB | 7.4 ms |
| 640 / 3 | 24.9M | 95 MiB | 8.1 ms |

### Results

**First runs (Nov 2026, the Mac's 9.4k battles, `small` from scratch, 8 epochs).** An epoch is 80 s on
the M3 (918 steps of 512); AWBC adds its ordered pass (~25 s). Both were still improving at epoch 8. On
the test battles (33,251 choices of 1700+ players, 55,743 of all), next to the self-play models
released then:

| | top-1 (1700+) | NLL (1700+) | top-1 (all) | value AUC |
|---|---|---|---|---|
| MiniPokeV2_1_1M (self-play) | 36.6% | 4.75 | 36.6% | 0.668 |
| MiniPokeV2_1_22M (self-play) | 39.7% | 5.39 | 39.8% | 0.692 |
| BC, small | 46.0% | 1.42 | 46.5% | 0.620 |
| AWBC (exp, beta 1), small | **46.6%** | **1.41** | 47.0% | 0.615 |

Paired evals, 4,000 games on 2,000 team pairs each (95% intervals):

| | vs heuristic | vs MiniPokeV2_1_1M | vs BC |
|---|---|---|---|
| MiniPokeV2_1_1M | 67.3% (65.9-68.7) | | |
| BC, small | 59.5% (58.1-60.9) | 47.5% (46.1-49.0) | |
| AWBC, small | 64.0% (62.6-65.5) | **53.6% (52.1-55.1)** | **55.1% (53.7-56.5)** |

The self-play models put almost no probability on many human choices (NLL 4.7 to 5.4): they played
another game than the humans. Advantage weighting, on the same data, beats BC everywhere and beat the
self-play model its size head to head. The value head learns slowly from human results (every battle's
~60 states share one result).

**`GladionV1_22M`** (released as minipoke v3, `MiniPokeV3_22M`) took two rounds of `large` on the GPU
box (Nov 15 to 21):
1. **`human-v21` (103k battles).** BC peaked at epoch 9 (val NLL 1.088, top-1 59.2%), then memorized
   and early stopping ended it at epoch 11. AWBC on the same data was stopped after epoch 6 (val NLL
   1.139), still improving, when the second dataset was ready.
2. **`human-v21b` (258k battles).** AWBC, `--epochs 6`, the defaults otherwise
   (`checkpoints/v3/awbc-large-v21b/offline.pt`). The best epoch is 5 (val NLL 1.065); epoch 6 was
   slightly worse. On its own test battles (Oct 9 to Nov 19, 1.54M choices) it scores top-1 59.95% and
   NLL 1.071 (1700+: 60.1%, 1.064), with value AUC 0.761.

On `human-v21`'s test battles (Oct 20 to Nov 15, 603,903 choices, 466,265 of them by 1700+ players),
which none of these models trained on:

| | top-1 (1700+) | NLL (1700+) | top-1 (all) | value AUC |
|---|---|---|---|---|
| MiniPokeV2_1_22M (self-play) | 39.8% | 5.28 | 40.2% | 0.723 |
| BC, `human-v21` | 59.4% | 1.079 | 59.2% | 0.752 |
| AWBC, `human-v21`, 6 epochs | 57.5% | 1.135 | 57.3% | 0.748 |
| **AWBC, `human-v21b` (`GladionV1_22M`)** | **60.0%** | **1.066** | **59.8%** | **0.757** |

2.5 times the battles moved top-1 by only 0.6 points. That figure undersells the gain: NLL cannot fall
below how unpredictable humans are, while strength can keep improving. Value AUC went from 0.62 (`small`,
the Mac's 9.4k battles) to 0.76 (`large`, 258k), on different test battles: a trend, not a measurement.
In games:
- Paired, 4,000 games on 2,000 team pairs (greedy), `GladionV1_22M` beat `MiniPokeV2_1_22M`, the best
  self-play model, **82.3%** (81.2-83.4%).
- Against SimpleHeuristicsPlayer, PPO's iteration 10 (107k states of PPO from it; the model itself was
  not measured alone) won 93.5% of 2,000 battles. `MiniPokeV2_1_22M` won 77.1% of 800.
- With search, `GladionV1_22M` plays at 1800+ on the ladder.

PPO from it (`--recipe v3`, still the provisional LR 2e-4, a reference KL of 0.05 to it, the BC model
of `human-v21` as a fixed pool opponent) made it worse. In training it won 39% against its starting
point at iterations 10 and 20 (100 games each), and 45% at iteration 30. Its KL to the starting point
grew to 0.2 by iteration 30 and stayed there: the anchor did not hold it. The heuristic win rate stayed
flat (90.5-94.5%). Paired, its iteration 60 (636k states) won **44.7%** (43.3-46.2%) against
`GladionV1_22M`. That is when self-play was dropped.

## Measuring

### On the human test battles: `offline-eval`

```bash
python train.py offline-eval --ckpt checkpoints/v3/bc-4m/offline.pt checkpoints/v3/awbc-4m/offline.pt \
    ../../src/ElsaMina.Battles/Models/GladionV1_22M --data data/human-v21 --out runs/v3/offline-4m.json
```

It measures checkpoints, or released models' folders (their ONNX, through `offline.OnnxPolicy`), on a
split (`--split`, default `test`: the newest battles): top-1 agreement and NLL against players rated
`--min-rating` (1700) or more and against all, the value's AUC at telling won from lost battles'
decisions, its MSE to the return, and its calibration (by value bin: mean value, mean return, win rate).
For a checkpoint with opponent heads, on a dataset with their targets, it adds their metrics
(`offline.opp_metrics`).
Agreement with humans is not strength: confirm in games.

### In games: `eval` and `elo`

```bash
python train.py eval --ckpt checkpoints/v3/awbc-4m/offline.pt --battles 4000 --processes 4      # vs the heuristic
python train.py eval --ckpt A --vs-ckpt ../../src/ElsaMina.Battles/Models/GladionV1_22M --battles 4000 --processes 4
python train.py eval --ckpt A --vs-ckpt A --search --processes 4        # A searching vs A
python train.py eval --ckpt A --vs-ckpt A --no-tera-before 5            # A without tera before turn 5 vs A
python train.py elo --ckpts A B ../../src/ElsaMina.Battles/Models/GladionV1_22M --scripted heuristic maxpower random \
    --processes 4 --out runs/elo.json
```

Models play greedy (the most likely legal action), each with its own encoder. `--ckpt`, `--vs-ckpt` and
`elo --ckpts` take checkpoints or released model folders. `eval` and `elo` play **paired teams**
(`paired.py`): each team pair from Showdown's own random battle generator is played twice, the teams
swapped the second time, and the 95% interval comes from the spread of the pair scores. Showdown's
random battle format generates its own teams, so these games are challenged as Anything Goes with custom
rules that make it a random battle (no team preview, HP in percent, Sleep Clause...; not Custom Game,
which reports exact HP to both sides). `--team-seed` picks the teams, the same ones for every match of a
round-robin; `--unpaired` plays independent random battles. A tie (Showdown's turn 1000 auto-tie, when
two greedy models loop) counts as half a win. `--processes P` splits the team pairs (`eval`) or the
matches (`elo`) over P processes on the same server.

`elo` fits Bradley-Terry ratings by maximum likelihood, the scripted players fixed as anchors so
ratings compare across round-robins (`--anchors` overrides them):

| Anchor | Rating |
|---|---|
| `random` (RandomPlayer) | 0 |
| `maxpower` (MaxBasePowerPlayer) | 410 |
| `heuristic` (SimpleHeuristicsPlayer) | 780 |

They were fitted over 2,500 battles per pair on a local Showdown (Oct 2026): the heuristic beat
maxpower 89.0% and random 99.1%, maxpower beat random 91.2%. Refit them after a Showdown update.

What to expect from the numbers (Oct 2026, measured between models):
- **Compare on 4,000 games or more** (+-1.5 points) before acting on gaps of a few points: two
  independent 1,500-game runs of the same pair once differed by 5.8 points.
- **Pairing helps less than expected between models**: the pair scores' spread is about 0.9 of
  independent games' (random battle outcomes depend more on the game than on the teams).
- **Seats don't matter**: a model against itself scored 50.2% over 3,000 games as the challenger.
- The heuristic and the old self-play models are now far weaker than `GladionV1_22M`: use it (with
  `--vs-search-depth 1` for its search) as the reference opponent, and the ladder as the final judge.

## Search at decision time

`search.py` (`eval --search`) plays a model with a one-turn lookahead on Showdown's own simulator,
scored by the model's value head. At a turn with two or more legal actions:

1. our candidates are the model's 3 most likely actions (`--search-top-k`), however unlikely
   (`--search-min-prob` 0): a model sure of a bad move still gets alternatives;
2. `search_sim.js` (Node, `--search-workers` processes) rebuilds the position in Showdown's simulator
   `--search-determinizations` times (2). Our team comes from the request: sets, exact HP, stats, PP,
   disabled moves. Each rebuild guesses the opponent's hidden sets with Showdown's random battle
   generator, made to agree with what was revealed (moves, ability, item, tera type). HP, status, boosts,
   tera, a few volatiles, hazards, screens, weather and terrain are written onto the battle. The
   opponent's unrevealed Pokémon are random sets it never switches to;
3. for each candidate and each of the opponent's actions (usable moves, with and without tera, and
   switches to its revealed Pokémon), one turn is played from that state;
4. each simulated turn is replayed into a copy of our poke-env battle and scored like the returns the
   value learned: the turn's faint reward (`--search-faint-reward`, 0.1) plus 0.995 x the model's value
   of the new state, or +-1 when the battle ended;
5. per guess, the turn is a simultaneous zero-sum game: regret matching finds the opponent's
   equilibrium mix, each candidate scores its expected value against it, the scores are averaged over
   the guesses, and the best candidate is played (0.02 x log policy probability breaks near ties).

`--search-depth 2` adds one more decision after the opponent's likeliest replies. Forced switches and
turns with a single legal action use the model alone, as does any turn whose search fails. A search
simulates about 40 to 50 turns: under a second, and 0.5 s in ElsaMina. Use `--processes`.

Measured on `MiniPokeV2_1M` (self-play, since removed), 600 team pairs each (Oct 2026):

| | Without search | Search (top 3) | Search, only candidates with 2%+ |
|---|---|---|---|
| vs the same model without search | 50% | **61.9%** (59.5-64.4%) | 57.2% (54.7-59.8%) |
| vs SimpleHeuristicsPlayer | 65.8% (63.4-68.3%) | **73.5%** (71.1-75.9%) | 74.0% (71.6-76.4%) |

Searching more (`--search-top-k 4 --search-determinizations 3` with the 2% threshold) gained nothing
then; none of these settings were measured again on `GladionV1_22M`.

**In ElsaMina** each model has a search variant, `-strategy <model>+search`: the same model and guard,
plus the search. The variants are offered when `ShowdownDirectory` in config.json points to a
pokemon-showdown package (`npm install pokemon-showdown@0.11.11`; in the Docker image,
`/opt/showdown/node_modules/pokemon-showdown`; locally, the clone itself) and Node runs (`NodePath`,
default `node`). `Strategies/GladionSearch` ports search.py: `ShowdownSimulator` runs the same
`search_sim.js` (copied next to the bot), `SearchPositionBuilder` is `position_of`, and each simulated
turn is scored by replaying the battle's lines plus the turn's through ElsaMina's own tracker, so the
model sees exactly the state it would see live. The search chooses among the actions
`GladionActionGuard` allows; past 8 s, or if anything fails, the bot plays the model's choice (after 5
failures in a row the simulator turns itself off until the bot restarts). With `LogLevel` Debug, the
log shows every decision the search changed. Keep search.py and the C# search in step.

## The observation and the model

**Encoders.** `encoder.py` (V1) turns a poke-env `Battle` into 12 Pokémon tokens' ids and features,
their moves, the field and the 14-action mask (6 switches, 4 moves, 4 moves with tera). Actions map
onto poke-env's `SinglesEnv` action space, so orders are always built by poke-env itself.
`encoder_v2.py` keeps V1's six arrays unchanged and adds, by flag (`encoder_for(cfg, priors)` picks a
model's encoder):

| Array | Shape | |
|---|---|---|
| `cand_ids` | int64 [6, K] | candidate move ids per opponent slot (rows = V1's slots 6-11), PAD = none |
| `cand_feats` | f32 [6, K, 40] | V1's 39 move features + fraction |
| `prior_ids` | int64 [6, 2, 4] | row 0: ability ids, row 1: item ids (V1 hashing), by weight then id |
| `prior_w` | f32 [6, 2, 4] | their weights |
| `prior_feats` | f32 [6, 24] | tera weights (20 types), has sets, n consistent / n roles, fallback, Choice possible |
| `hist_ids` (`history`) | int64 [2] | last move used by our active, by theirs (poke-env `Pokemon.last_move`) |
| `hist_feats` (`history`) | f32 [8] | turns since their / our active came in, they / we switched by choice last turn, their / our protect counter, they chose the same move twice in a row, that and a Choice item possible |
| `effect_feats` (`move_effects`) | f32 [12, 4, 6] | per known move, against the opposing active: no effect, ability blocks, status fails, immune before, failed last time, failed the last two times |
| `cand_effect_feats` (`move_effects`) | f32 [6, K, 6] | the same per candidate move, against our active |
| `event_ids` (`history_tokens K`) | int64 [K, 2] | move id (PAD for an entrance), species id of the last K events, oldest first |
| `event_feats` (`history_tokens K`) | f32 [K, 14] | present, ours, move, entrance, dragged, called/locked, crit, miss, super effective, resisted, immune, failed, KO, turns ago |
| `matchup_feats` (`matchup_tokens`) | f32 [12, 16] | our mon i vs their active, our active vs their mon j: expected damage of each known move both ways (`damage.py`), max each way, KO flags, move order |

The released observation (V2.1, `GladionV1_22M`) is V2 with `history` and `move_effects`: the current
turn, plus the summaries above of the last turn or two. History and move effects read poke-env's state
(`last_move`, `_active_turns`, `protect_counter`) and the battle's protocol log (`Battle._replay_data`,
every parsed line but `|request|`), so the encoder stays a function of the battle: re-encoding recorded
fixtures from their lines reproduces every array. `history_tokens` (the last K events as tokens) and
`matchup_tokens` exist in Python only: ElsaMina's encoder refuses a spec using them, so a model trained
with them needs those arrays ported first. Under self-play neither gained anything; neither has been
tried on human data. A dataset is built with one observation: `human_data.py --encoder-from` a
checkpoint or model folder with the flags wanted.

**Set priors.** `setpriors.py` loads Showdown's gen 9 random battle sets
(`data/random-battles/gen9/sets.json` in the clone). Each species has a few roles, each with a movepool,
abilities and tera types; **the gen 9 file has no items** (Showdown's `teams.ts` picks them in code), so
the item prior is empty with it. For each revealed opponent Pokémon:

- **Species**: its id, else its dex `battleOnly` form(s), then `baseSpecies` (Palafin-Hero → palafin).
- **Evidence**: revealed moves, its revealed base ability, revealed item, and its tera type once
  terastallized. Evidence outside the species' whole pool says nothing about its roles (Struggle, moves
  called by Sleep Talk, Trace, Illusion...) and is ignored. A role is consistent when it contains every
  remaining piece. No consistent role → every role of the species ("fallback").
- **Candidates**: moves of the consistent roles not revealed yet, ranked by the fraction of consistent
  roles containing them (descending), ties by move id (ascending), the first K = 8 kept. They go through
  the same move embedding and features as known moves, plus a known/candidate flag and that fraction.
- **Abilities, items, tera types**: weight of x = mean over consistent roles of [x in role] / |role's x|
  (a revealed one: 1), as a weighted embedding sum (abilities, items) and a weighted multi-hot (tera).
- **Choice item possible**: a revealed Choice item, or an unknown item, no revealed status move other
  than Trick / Switcheroo / Healing Wish, and a consistent role that can be dealt a set without status
  moves (as `teams.ts` hands out Choice items).

Checkpoints and datasets store the priors they were built with (Showdown's sets change over time), and
exports write them to `set_priors.json`; every V2 model so far shares the same priors (f05fb3c538a6).

**Move effects.** V1's move features read poke-env's type chart only, which ignores abilities
(Earthquake into Levitate looks super effective). `effects.py` adds, for each move against the opposing
active: **no effect** (the type chart, a revealed Air Balloon or Safety Goggles, or the target's types
make it a sure miss), else **ability blocks** (the probability that the target's ability stops it: its
revealed one, else its set priors' weights, else its possible abilities, times the probability that the
owner is not a Mold Breaker); **status fails** (a major status into a target that has one); and from
the protocol log (`history.move_outcomes`) **immune before**, **failed last time / twice**. Every rule
is in the spec as data.

**The model** (`model.py`). Tokens `[actor, critic, field, 6 ours, 6 theirs]`: state tokens attend to
each other, actor and critic to the state and themselves. With V2, each opponent's candidate moves go
through the move MLP (with a known/candidate flag and their fraction), and their fraction-weighted mean,
the ability and item prior embeddings and the prior features join its token. The policy head is a
pointer head: switch logits from our 6 Pokémon tokens, move logits from the active Pokémon's 4 move
vectors (normal and tera). The value head is scalar or 51-bin distributional; `forward()` always returns
its expected value. Attention is `scaled_dot_product_attention` with a boolean mask (MPS, CUDA and ONNX
export). Architecture options for a new model: `--move-tokens` (one token per Pokémon and move slot; the
move head reads our active's move tokens after the transformer) and `--typed-ffn` (one feed-forward
block per token type). They change no observation, and gained nothing under self-play.

**Move pooling (`--move-pool`, not measured yet).** Without move tokens, a Pokémon token reads its 4
known move vectors end to end (`concat`, 4 x d_model inputs: Gladion v1's). The slots come from poke-env's
`mon.moves` order: the request's for our Pokémon, the order they were revealed in for the opponent's, so
the same set gives different tokens depending on the order it was seen in. `meanmax` reads their mean
and max over the slots that hold a move instead (2 x d_model; zero for a Pokémon with no known move),
the same in any order. The move head still scores each slot, so the actions don't change. `large` with
the V2.1 observation has 1.05M fewer parameters. The export's inputs, outputs and `encoder_spec.json`
are the same either way. An option for new models: `--init` keeps its checkpoint's.

**Opponent heads (`--opp-heads`, training only, not measured yet).** A re-simulated replay holds both
sides' requests and choices, so each decision can also learn what its player could not see
(`opp_targets.py`). Showdown sends both sides a request in each batch (a `wait` one to the side with
nothing to choose), so the k-th requests of the two sides pair up; a battle whose sides have different
counts gets no choice targets. The targets, in our view's terms:
- the opponent's choice from the same state: switch to one of our view's 6 opposing slots (0-5), to a
  Pokémon not revealed yet (6), its active's known move j (7-10), candidate move k from the set priors
  (11 to 10 + K; K = 0 in V1), or any other move (11 + K). None for a wait, a recharge, or a move while
  our view has no opposing active. And whether it terastallized, when it could;
- each revealed opposing Pokémon's real item (hashed as the encoder does), ability, tera type and
  moves, from the opponent's first request (items not consumed yet).

The heads read the final tokens: a query from the actor and critic tokens; a pointer over the 6
opposing Pokémon tokens for switches, and over their active's known and candidate move vectors (before
the transformer) for moves, classes our view rules out masked; one linear for "a Pokémon not revealed
yet" and "another move"; and per opposing token, linear heads over items, abilities, tera types and
moves. The loss adds `--opp-coef` (0.5) x the mean of their six cross-entropies, each divided by
log(classes) (about 1 at init, like the value loss). `forward()` never reads them: the export is the
same model's without them (same ONNX inputs, outputs and weights, same `encoder_spec.json`). `--init`
adds fresh heads to a checkpoint, e.g. to fine-tune `GladionV1_22M` with them. Each epoch logs their
loss (`opp`) and the opponent's choice top-1 on validation; the test metrics and `offline-eval` add
the choice's top-1 and NLL, tera accuracy, top-1 on the items and abilities not revealed yet, tera
type top-1, and recall@4 of the moves not revealed yet (the 4 likeliest among those our view does not
show). They are not the auxiliary heads of the V2 ablations (self-play, Oct 2026), whose `aux_*`
weights old checkpoints drop when they load.

**`encoder_spec.json`.** `export_onnx.py` writes, next to `gladion.onnx`, everything the C# encoder
needs: vocabularies, item hashing, one-hot orderings, feature sizes, and for V2 `"encoder_version": 2`,
every ONNX input in order with its shape, the config fields that shape the observation, the set priors
file and its sha256, and every rule of the history and move effect features (including what the C#
tracker must keep beyond poke-env's state). The outputs are `logits [B, 14]` and `value [B]`.
`tests/test_export_contract.py` checks the contract.

## Using a model in ElsaMina

Each exported model is named after its **version** and its size in parameters: `GladionV1_22M`
(strategy `gladionv1-22m`). A Gladion version is a model generation: v1 is the first trained on human
replays, a new major version changes the training or the observation, and a minor version adds input
flags to its version's observation (written `1_1` in names, `gladionv1.1-...` as a strategy). The
observation has its own version, the encoder's: `encoder.py` is V1, `encoder_v2.py` V2, and
`encoder_v2.py --move-effects` V2.1, which Gladion v1 reads. (minipoke numbered its models after the
observation, `MiniPokeV2_1_22M` for V2.1, until v3 numbered a training change: see [History](#history).)
Models of one encoder share their C# encoder: `GladionV1_22M` plays with `GladionEncoderV2`, which reads
the flags from the spec. ElsaMina keeps the models worth playing: today `gladionv1-22m`, alone or
`+search` (`-strategy gladion` picks the latest model); the bot starts with `gladionv1-22m+search`, and
`-strategy calc` switches to the damage calc strategy the models fall back on.

The C# side reproduces what the model sees in training:

| Python | C# |
|---|---|
| poke-env's battle tracking | `src/ElsaMina.Battles/Tracking` (shared by every version) |
| `encoder.py`: slots, ids, item hashing, action mask | `Strategies/Gladion/GladionEncoder.cs` (shared) |
| `encoder.py`: feature vectors | `Strategies/GladionObservationV1/GladionEncoderV1.cs` |
| `encoder_v2.py`: V1's arrays, then set priors and history | `Strategies/GladionObservationV2/GladionEncoderV2.cs` (extends V1's) |
| `setpriors.py`, `history.py` | `Strategies/GladionObservationV2/SetPriors.cs`, `ProtocolHistory.cs` |
| `effects.py`, `history.move_outcomes` | `Strategies/GladionObservationV2/MoveEffects.cs`, `MoveOutcomes.cs` |
| `export_onnx.py` outputs | `src/ElsaMina.Battles/Models/GladionVN_SIZE/` (one per model) |
| ONNX inference, action → `/choose` | `Strategies/Gladion/` (shared) |

Any difference between the two encoders is silent: the model still plays legal moves, just badly. The
parity fixtures catch it. Never "fix" a tracker quirk toward Showdown's truth: it must match poke-env.

Two things happen around the model:
- **The guard** (`Strategies/Gladion/GladionActionGuard.cs`). The bot plays the most likely legal
  action, unless the guard vetoes it, then the most likely one left (each override is logged). It vetoes
  what is sure to be wasted: a move our active Pokémon already used on the opposing active with no
  effect (`|-immune|`), while neither terastallized since (Tera Blast, Weather Ball, Endeavor and the
  like excepted); a move that only heals at full HP; Sleep Talk and Snore while awake; a hazard the
  opponent's side already has every layer of; and every switch after Showdown answered "Can't switch:
  The active Pokémon is trapped", until either active Pokémon changes. The parity fixtures compare the
  model's own argmax, without the guard; `analyze_replays.py` applies the same rules (`guard_vetoes`):
  change both together.
- **The value calibration** (`value_calibration.json` next to the model, optional).
  `analyze_replays.py --write-calibration` fits expected return = scale x value + offset on the ladder
  battles and writes it; ElsaMina logs the expected return next to the value. Nothing plays from it.

### Replacing a released model

A model trained on the same observation as a released one (same encoder flags and set priors) exports
the same `encoder_spec.json`, so only `gladion.onnx` changes:

```bash
python export_onnx.py --ckpt checkpoints/<run>/offline.pt --out export/<run>
cp export/<run>/gladion.onnx ../../src/ElsaMina.Battles/Models/GladionV1_22M/gladion.onnx
git diff --stat ../../src/ElsaMina.Battles/Models/GladionV1_22M/   # only gladion.onnx
python make_fixtures.py --version 1_22M --battles 20                # Showdown running
```

The last step is required: the parity fixtures also store the logits of the model they were recorded
with (`GladionEncoderV2ParityTest` compares them with ElsaMina's onnxruntime). To keep the old model available
side by side instead, release the new one under its own name.

### Releasing a new model N_SIZE

`N_SIZE` is the version and the size: `1_4M` for a ~4M v1 model (strategy `gladionv1-4m`), `1_1_22M`
for a minor version adding input flags to v1's observation (strategy `gladionv1.1-22m`), `2_22M` for the
next generation (strategy `gladionv2-22m`).

1. Export the checkpoint into its model directory:
   ```bash
   python export_onnx.py --ckpt checkpoints/<run>/offline.pt --out ../../src/ElsaMina.Battles/Models/GladionVN_SIZE
   ```
2. Record parity fixtures (Showdown running): `python make_fixtures.py --version N_SIZE --battles 20`.
3. On the C# side:
   - add `GladionVN_SIZE` to `BattleDecisionStrategy`, and `GladionVN_SIZE_Search` after the other
     search variants (`RegisterGladion` registers it with the model);
   - add `RegisterGladion<...>(builder, BattleDecisionStrategy.GladionVN_SIZE);` to `BattlesModule`,
     with the encoder of its observation (`GladionEncoderV2` for V2.1's). New input arrays mean porting
     them to the encoder first;
   - add the model to `MODELS` in `GladionEncoderV2ParityTest` (or a new version's copy of it).
4. Run the tests. The parity test must pass for the model: every array equal to what the Python encoder
   produced, every action mapped to the order poke-env sent, and the same logits in Python's
   onnxruntime and ElsaMina's.

Upgrading poke-env, or changing what an encoder reads from it, means checking the C# tracker against
new fixtures too.

## Ladder replays

`-savereplays` keeps, for each ladder battle, every line the bot received (`<id>.log`) and Showdown's
public replay (`<id>.json`, with the input log). `analyze_replays.py` replays each log into poke-env,
runs the model on every decision point and writes a report:

```bash
python analyze_replays.py replays/replays --out replays/analysis      # ~20 s, cached after
python analyze_replays.py replays/replays --out replays/analysis --write-calibration
```

`report.md` has the key findings, results by opponent rating, how battles ended, the parity check (the
committed choices against the argmax of the model that played, with or without the guard, or among the
search's candidates), the value head and its calibration, and the bot's behavior next to the humans'
(moves into immunities, failed moves, tera timing...). `battles.csv`, `decisions.jsonl` and
`value_calibration.json` hold the numbers. The replay folder is only read. `PLAYED_BY` at the top of the
script names the model the report is about (`GladionV1_22M`); `COMPARE_WITH` (none) names another
released model to run on the same states.

## Model playground

`playground/` is a static page that runs a released model in the browser (onnxruntime-web 1.20.1 from
jsDelivr, wasm) on the battle states of its parity fixtures, and lets you edit them:

```bash
python3 -m http.server 8765        # from the repository root
open http://localhost:8765/tools/gladion/playground/
```

Pick a model and one of its fixtures, or drop any fixture `.json.gz` on the page. Step through the
decisions with the slider or the arrow keys: the page shows the battle log so far, the 12 Pokémon, the
field and the v2 inputs, the 14 action probabilities and the value (the expected return, not a win
probability). Edits (HP, status, boosts, weather, hazards, the action mask, any raw input cell) re-run
the model at once; an edited input is not re-encoded. The page never encodes a battle itself: every
input comes from a fixture.

## History

What was tried, in order (Oct to Nov 2026), and what it gave. The code of the abandoned approaches is in
git history.

Until Nov 23 the project was **minipoke**. Its versions V1, V2 and V2.1 numbered the observation (the
encoder; they still name it), its models were named after them (`MiniPokeV2_1_22M`: observation V2.1,
22M parameters), and v3 numbered the switch to human replays while keeping V2.1's observation. minipoke
v3 is Gladion v1; no earlier minipoke model is still released.
[docs/minipoke-v2-architecture.svg](docs/minipoke-v2-architecture.svg) draws the model of minipoke v2
(`MiniPokeV2_1M`).

| When | Approach | Result |
|---|---|---|
| Oct 20 | **minipoke V1**: ps-ppo shrunk for a Mac. BC on SimpleHeuristicsPlayer, then mirror self-play PPO (`MiniPokeV1_1M`, `V1_4M`) | about 61% vs the heuristic (small) |
| Oct 22-24 | **V2 ablations** on small at matched states: random battle set priors, history features, move tokens, history event tokens, matchup tokens, typed FFN, auxiliary heads, win/loss reward, bigger sizes, a longer recipe (opponent pool, LR schedule, 51-bin value) | set priors and history features beat V1_4M (53.9% over 1,000 team pairs); everything else added nothing |
| Oct 24 | `MiniPokeV2_1M` (set priors + history), first ladder run | 47.9% over 535 battles, performance rating 1109; value head 0.18 too optimistic against humans; moves repeated into revealed immunities |
| Oct 27 | **V2.1**: move effect features (ability, item and type immunities, earlier failures), and the guard | fewer wasted moves (not measured on the ladder alone) |
| Oct 28 | **One-turn search** on Showdown's simulator | +11.9 points against the same model (61.9%); 73.5% vs the heuristic (65.8% without) |
| Nov 4-11 | V2.1 at **22M** (`large`, distilled from the 4M, 10M PPO states on the GPU box) | 55.8% vs its 4M teacher, +5.8 points for 5x the parameters; 15M states no better (48.1%); about 1300 on the ladder |
| Nov 11 | Diagnosis of the plateau | PPO barely moved the policy (median KL 0.0027 per iteration, target 0.03) and the heuristic win rate was flat after 1M states: the training signal, not size or time, was the limit. Options ranked: training on the search's choices, bigger PPO steps, and human data as the long shot (Metamon: offline RL on human replays reached human-level play) |
| Nov 11-13 | **Human replays**: public replays carry input logs, re-simulated on their own server version | all 9,358 replays of the first batch replay exactly; 558 of 561 of the bot's games identical to what ElsaMina received |
| Nov 13 | **Offline RL**, small, 9.4k battles: BC, then AWBC (the plan: a Q head only if AWBC did not beat BC) | AWBC beat BC on every metric and beat the self-play `MiniPokeV2_1_1M` head to head (53.6%); BC lost to it (47.5%). The Q head was never needed |
| Nov 15-21 | `large` on the GPU box: BC and AWBC on 103k battles, then AWBC on 258k: **minipoke v3, `MiniPokeV3_22M`** (`minipokev3-22m`) | test top-1 59.95%; beat `MiniPokeV2_1_22M` 82.3%; PPO from it lost to it (44.7% at iteration 60); with search, 1800+ on the ladder |
| Nov 22 | Self-play PPO, the BC-from-heuristic and distillation commands, the Q head and the self-play models removed | |
| Nov 22 | **Move pooling that ignores slot order** (`--move-pool meanmax`) and **training-only opponent heads** (`--opp-heads`: the opponent's choice and real sets, from both sides of the re-simulations) | not measured yet |
| Nov 23 | **minipoke renamed Gladion**: minipoke v3 becomes Gladion v1 (`GladionV1_22M`, strategy `gladionv1-22m`, same weights), the code moves to `tools/gladion` and `Strategies/Gladion*` | |

Never tried: training the network on the search's choices (expert iteration), and the PPO step-size
ablation (bigger learning rate, more epochs, bigger batches) planned for minipoke v3 before the human
data made it moot.

## Files

- `collect_replays.py` downloads public human replays with their input logs.
- `showdown_versions.py` keeps a built Showdown checkout per server version; `resim.js` re-simulates
  input logs on one; `resim.py` judges the re-simulations (`check`) and checks them against the bot's
  own `.log`s (`parity`).
- `human_data.py` turns human replays into the dataset; `dataset.py` holds the sharded on-disk format
  and its streaming loader; `opp_targets.py` reads the opponent heads' targets (the opponent's choice
  and real sets) from a re-simulation.
- `encoder.py` (V1) and `encoder_v2.py` (set priors, history, move effects, events, matchups, by flag);
  `setpriors.py`, `history.py`, `effects.py`, `damage.py` (the matchup damage estimate).
- `model.py`: `GladionTransformer`, `ModelConfig`, the presets. `checkpoint.py` saves and loads
  checkpoints (weights, config, optimizer, set priors).
- `offline.py`: BC and AWBC, the value loss, the offline metrics, `OnnxPolicy` (a released model as a
  policy).
- `train.py`: the CLI (`offline`, `offline-eval`, `eval`, `elo`). `runtime.py`: logging, the Showdown
  launcher, MPS fallback detection, memory reporting.
- `players.py`: `InferenceBatcher` (batches the decisions of concurrent battles) and `ModelPlayer` (plays
  a model greedily). `matches.py`: scripted players and matches with a watchdog. `paired.py`: paired
  teams. `elo.py`: the round-robin fit. `search.py` and `search_sim.js`: the one-turn search.
- `export_onnx.py` exports a checkpoint (ONNX, `encoder_spec.json`, `set_priors.json`).
  `make_fixtures.py` records battles for ElsaMina's parity tests.
- `analyze_replays.py` analyzes the bot's own ladder games.
- `playground/` runs a released model in the browser on its fixtures.
- `tests/` (pytest): the export contract, the encoders on offline poke-env battles (priors, history,
  events, matchups, move effects, action mapping), the model (move pooling, the opponent heads and
  their targets), the dataset, offline RL and its metrics,
  paired teams, the search, re-simulation and parity on committed fixtures, replay collection and
  analysis. `python -m pytest -q`. The re-simulation tests skip without the Showdown version they need.

