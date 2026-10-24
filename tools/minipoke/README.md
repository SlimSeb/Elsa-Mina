# minipoke: a small PokeTransformer for an M3 Mac

A proof of concept based on [Nebraskinator/ps-ppo](https://github.com/Nebraskinator/ps-ppo),
shrunk to run on Apple Silicon with [poke-env](https://github.com/hsahovic/poke-env)
and a **single** local Showdown server. ElsaMina plays with the exported models
(`-strategy minipokev1-4m`, see [Using a model in ElsaMina](#using-a-model-in-elsamina)).
The next model, **minipoke-v2**, adds random battle set priors and optional history,
move, matchup and auxiliary features, all behind flags (see [minipoke-v2](#minipoke-v2-encoder-v2)).

| | ps-ppo | minipoke `small` (V1_1M) | minipoke `wide` (V1_4M) | minipoke `medium` |
|---|---|---|---|---|
| Parameters | ~16M | 0.91M | 4.12M | 3.85M |
| d_model / layers / heads | 512 / 3 / 8 | 128 / 3 / 4 | 256 / 3 / 8 | 256 / 4 / 8 |
| FFN | x4 | x2 | x4 | x2 |
| Embeddings (species / item / ability / move) | 64 / 32 / 64 / 64 | 32 / 16 / 16 / 32 | 64 / 32 / 64 / 64 | 64 / 32 / 32 / 64 |
| Attention | `flex_attention` (Triton, CUDA-only) | `scaled_dot_product_attention` + boolean mask (MPS ok) | same | same |
| Turn history | KV cache, 64 turns | none (current state only) | none | none |
| Policy head | linear on actor token | pointer heads: switch logits from team tokens, move logits from the active Pokémon's move vectors | same | same |
| Value head | 51-bin distributional | scalar | scalar | 51-bin two-hot over [-1.6, 1.6] |
| Environment | many Showdown servers + Ray workers | one server, poke-env, asyncio, batched inference | same | same |
| Training | BC → PPO | BC → mirror self-play PPO | same as `small` | BC (or distillation) → PPO with an opponent pool, schedules, resumable |

`small` and `wide` are the architectures of the released `MiniPokeV1_1M` and
`MiniPokeV1_4M`. Every V1 size exports the **same ONNX interface** (inputs, dtypes,
shapes, `logits [B,14]`, `value [B]`) and the same `encoder_spec.json`, byte for byte:
the distributional head returns its expected value, so ElsaMina cannot tell them apart.
`tests/test_export_contract.py` checks this. A minipoke-v2 model keeps the outputs and
V1's six inputs, and adds its own inputs after them.

## Setup (macOS, Apple Silicon)

Everything runs from this directory. The virtual environment, checkpoints,
exports and the Showdown clone stay here, ignored by git.

```bash
cd tools/minipoke

# 1. Python env
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt          # torch from PyPI includes MPS on macOS
pip install -r requirements-dev.txt      # + pytest, matplotlib, psutil

# 2. One local Showdown server (current Showdown needs Node 22+)
git clone https://github.com/smogon/pokemon-showdown.git
cd pokemon-showdown && npm install && cp config/config-example.js config/config.js
node pokemon-showdown start --no-security   # leave running, port 8000
```

If you push concurrency high, raise `exports.subprocesses` in Showdown's
`config/config.js` so battles are simulated across several CPU cores. That
is still one server, just with more simulator processes.

poke-env is pinned on purpose: ElsaMina's battle tracker is a port of that exact
version (see below). minipoke-v2 also reads Showdown's random battle sets from that
clone (`--sets` points elsewhere).

## Run

```bash
# Phase 1: imitate SimpleHeuristicsPlayer
python train.py imitate --size medium --battles 3000 --epochs 8
#   or distil an already trained model (its full action distribution and value)
python train.py distill --teacher checkpoints/ppo_latest.pt --size medium --battles 3000

# Phase 2: PPO from that checkpoint, with a budget in training states (or --hours, --iterations)
python train.py ppo --init checkpoints/bc.pt --states 5000000 \
    --showdown-cmd "node pokemon-showdown start --no-security"

# Stop it (Ctrl-C, SIGTERM, or `touch runs/<run>/STOP`), then continue later
python train.py ppo --resume checkpoints/ppo_latest.pt

# Evaluate (greedy, 1000 battles, 95% Wilson interval), head to head, league table
python train.py eval --ckpt checkpoints/best.pt --opponent heuristic
python train.py eval --ckpt checkpoints/best.pt --vs-ckpt other/best.pt
python train.py elo --ckpts a/best.pt b/best.pt --scripted heuristic maxpower random

python plot_metrics.py runs/<run>                     # -> runs/<run>/metrics.png
python plot_metrics.py runs/a runs/b --out ab.png     # overlay runs, x = total_states
python export_onnx.py --ckpt checkpoints/best.pt --out export/
```

`plot_metrics.py` plots against `--x total_states` (default), `iteration` or `hours`,
and overlays every run given.

### Sizes and their defaults

`--size` picks the architecture of a new model and the defaults of every option
not given on the command line. `--init` and `--resume` always keep the
architecture stored in the checkpoint, and default to the options of the size it
matches. Checkpoints store their own config, so older ones (saved before the value
head was configurable, or by the previous `train.py`) still load, resume and export.

| | `small`, `wide` (the V1 recipe, unchanged) | `medium` |
|---|---|---|
| Value head | scalar, MSE | 51 bins, two-hot cross-entropy / log(51) (`--value-bins`, `--value-ce-norm`) |
| BC value fit (`--bc-value-coef`) | 0 (policy only) | 0.5, to the battles' discounted returns |
| LR (`--lr`, `--warmup-iters`, `--lr-min-ratio`) | 2e-4 constant | 1e-4 peak, 10-iteration warmup, cosine to 10% |
| Value loss weight (`--vf-coef`) | 0.5 | 0.5 (0.1 with `--value-ce-norm none`) |
| Entropy coef. (`--ent-start`, `--ent-end`) | 0.01 constant | 0.01 → 0.003, linearly |
| KL early stop (`--kl-stop`) | last minibatch's approx KL | the epoch's mean approx KL |
| Opponents (`--mirror-share`, `--pool-share`, `--heuristic-share`) | 100% mirror | 70% mirror / 20% pool / 10% heuristic |
| Pool sampling (`--pool-sampling`) | (no pool) | `pfsp` |
| `--concurrency` / `--battles-per-iter` | 32 / 128 | 48 / 192 |
| Iteration budget | 300 (when none of `--iterations`, `--hours`, `--states` is given) | same |

Every other PPO hyperparameter (`PPOConfig`: gamma 0.995, λ 0.95, clip 0.2,
3 epochs, minibatch 1024, target KL 0.03) is shared. Options added since a run
started keep their old behaviour when it is resumed (`--value-ce-norm none`,
`--kl-stop last`, `--pool-sampling uniform`).

**The value loss scale (`--value-ce-norm`).** The two-hot cross-entropy is divided by
log(value_bins): a fresh head (uniform logits) scores exactly 1, the scale of the
MSE of ±1 returns, so `--vf-coef` means the same for both heads. The 0.1 medium used
until now compensated for the raw loss. **Start medium at `--vf-coef 0.5`** (the new
default): the gradient of the raw cross-entropy is softmax - target, so 0.5 on the
normalized loss is 0.5 / ln 51 ≈ 0.13 on the raw one, close to the 0.1 that took the
medium run from 43% to 55%, and 4x below the raw 0.5 (≈ 2.0 normalized) that stalled
it. It is also V1's weight, so the value weight no longer differs between the two
recipes. If the value gradient still dominates the trunk (see below), try 0.25.
`--value-ce-norm none` brings back the raw loss and medium's old 0.1.

### Distillation and the BC dataset

`distill` plays teacher-vs-teacher battles (the teacher samples its own policy, and
plays a uniformly random legal move with probability `--epsilon`, 0.05). It records the
teacher's whole softmax and its value, then trains the student on the cross-entropy to
that distribution (KL to the teacher plus a constant). It also trains
`--vf-coef` × value loss toward the teacher's value (`--value-target return` uses the
battle's real returns instead). Validation reports the KL and the argmax agreement.

The teacher and the student may use **different encoders**: the teacher plays from its
own encoding of each state, the dataset stores the student's encoding of the same
state (every encoder shares V1's action space and mask). That is how a minipoke-v2
model starts, from a V1 checkpoint rather than from heuristic BC:

```bash
python train.py distill --teacher checkpoints/v1-small.pt --size small --encoder-version 2 \
    --battles 10000 --epochs 6 --data data/distill-v2 --ckpt-dir checkpoints/v2-priors
```

`imitate` and `distill` write their samples to `data/<name>/` as compressed `.npz`
shards: float16 features, int16 ids, a bit-packed mask. A V1 sample is about
110-130 bytes on disk (v2: about 160-190), against ~12 KB as a float32 numpy dict.
That's ~5 KB per battle, so 10k battles take about 50 MB. Training streams the shards
(a few decoded at a time, shuffled together), so RAM stays flat whatever the dataset
size. `meta.json` records the observation layout. Use `--data DIR` to pick the
directory; a directory that already holds a dataset is reused without playing (e.g. to
retrain with other epochs, or another student with the same encoder flags: move
tokens and typed FFN do not change the encoding, history and matchups do).

### Long runs

- **Budget.** `--hours H`, `--iterations N` and `--states S` are *totals for the run*.
  Training stops at whichever comes first (after the iteration that crosses it).
  Resuming counts the time and states already spent. The cosine LR and the entropy
  annealing follow the progress `max(iterations / N, hours / H, states / S)`, so two
  runs with the same `--states` anneal at the same states however fast they play.
  Raising a budget on `--resume` extends the run, and the schedules stretch to match.
- **Stopping and resuming.** The first Ctrl-C / SIGTERM / `runs/<run>/STOP` finishes the
  current iteration, saves, and exports. A second Ctrl-C quits at once.
  `checkpoints/ppo_latest.pt` is rewritten (atomically) after every iteration. It
  holds the model, optimizer, schedule progress, iteration, states, RNG states,
  pool manifest (with win rates), Elo table, best score and every option, so `--resume`
  continues exactly (any option given again on the command line overrides the saved one).
- **Opponent pool.** Every `--snapshot-every` iterations the policy is saved to
  `checkpoints/pool/` (cap `--pool-cap` 20). The starting policy is the first snapshot.
  When the pool is full, the oldest and the 5 newest snapshots stay, and among the
  others the one leaving the smallest gap goes, which keeps a spread of ages. Each
  iteration samples `--pool-slots` (2) snapshots. Each slot has its own inference
  model and batcher, while all learner seats share one. Only the learner's side of
  pool and heuristic battles is trained on.
  `--pool-sampling pfsp` (medium's default) draws them with weight
  max(1 - w, `--pfsp-floor` 0.1), w the learner's win rate against that snapshot: an
  EMA over its training games (`--pool-ema` 0.05 per game, 0.5 before any), so the
  snapshots it beats least are played most and every one keeps being played.
  `uniform` is V1's draw.
- **Evaluation.** Every `--eval-every` iterations: greedy win rate against
  `SimpleHeuristicsPlayer` (`--eval-battles` 200) and against the 3 newest snapshots
  (`--eval-pool-battles` 100 each). A candidate for `checkpoints/best.pt` (an evaluation
  with at least `--min-eval-battles` 150 finished battles, beating the best) is
  re-evaluated over `--confirm-battles` (400) fresh battles, and only that confirmed
  win rate competes: the best of many noisy evaluations is optimistic. The win rate
  that made `best.pt` win is optimistic too, so `best.pt` itself is also played again
  over 400 fresh battles at each challenge, and the candidate must beat that (its
  fresh battles add up across challenges; `incumbent_wr` in metrics.csv). Without
  that, a lucky early `best.pt` outlived every later policy (one ablation run kept
  iteration 10 of 377).
  `--confirm-battles 0` turns both off. At the end, `best.pt` (or the latest
  checkpoint if there is none) is exported to `export/`.
- **Elo.** See [Elo league table](#elo-league-table): `runs/<run>/elo.json`, `elo` in
  metrics.csv.
- **Robustness.** A battle that raises in `choose_move` plays a random move and the
  error is logged. If no battle finishes for `--stall-timeout` seconds (180), or an
  iteration takes over 4× the recent median, the iteration is cancelled. Its finished
  battles are still used, the players reconnect with fresh websockets, and the run
  goes on. With `--showdown-cmd`, the server is started if it is down and restarted
  if it dies (its output goes to `runs/<run>/showdown.log`). After 5 failed iterations
  in a row the run saves and stops.
- **Checkpoints on disk.** `ppo_latest.pt`, `best.pt`, the last 5 `ppo_NNNNN.pt`
  (every `--save-every` iterations) and every 50th. A medium checkpoint with Adam
  state is ~45 MB (wide ~50 MB), and a pool snapshot ~15 MB.
- **Logs.** `runs/<run>/train.log`, plus one row per iteration in
  `runs/<run>/metrics.csv`: states, st/s, losses, entropy, KL, PPO epochs run, clip
  fraction, explained variance, LR, entropy coefficient, faint reward, training win
  rates by opponent, eval and confirmed win rates, best and best.pt's re-evaluated
  win rate, Elo, pool size and its hardest
  snapshot's EMA win rate, auxiliary losses and accuracies, uniform KL, stalls and RSS.

Training fixes, besides the value loss scale:
- `--kl-stop mean` stops the PPO epochs when the epoch's mean approx KL exceeds the
  target (one minibatch is noisy). `small` keeps V1's last-minibatch rule.
- `--faint-anneal-states N` takes the faint reward linearly from 0.1 to 0 over N
  training states (off by default).

How it runs:
- **Training** (BC and PPO updates) uses `--device auto`: MPS on an M3, CUDA on a
  Linux GPU box. Training on MPS keeps `PYTORCH_ENABLE_MPS_FALLBACK=1`, but every
  operator that falls back to the CPU is logged as `[mps-fallback]`. A forward and
  backward self-check at startup reports it before the run gets going.
- **Rollout inference** uses `--infer-device auto` for `ppo`: it times a forward pass on
  the CPU and on the training device at rollout batch sizes and keeps the faster one
  (`[bench]` lines in the log). Batches are capped at `--max-infer-batch` 64: on a CPU,
  the medium model's throughput peaks there and drops for larger batches. On an M3,
  MPS beats the CPU for `wide` once batches reach ~32 states (about 6 ms vs 12 ms at 64).
- `--concurrency` sets concurrent battles per seat pair. With `medium`, the learner
  plays about `concurrency` battles at a time, split across the opponent types.

### A long run on the M3 (16 GB)

```bash
python train.py imitate --size medium --battles 10000 --epochs 6      # ~50 MB of data
python train.py ppo --init checkpoints/bc.pt --hours 24 \
    --showdown-cmd "node pokemon-showdown start --no-security"
python plot_metrics.py runs/<run>
```

Raise `exports.subprocesses` in Showdown's `config/config.js` (e.g. 6-8 simulator
processes): rollouts are mostly Showdown and poke-env time. Run a 20-minute
`ppo --hours 0.33` first and read `st/s` and the `play`/`learn` split. Total states ≈
st/s × 3600 × hours. At 150 st/s, 24 h is about 13M states.

Measured so far, on a 16-thread x86 laptop CPU with **no GPU**, training on the CPU
(not an M3; MPS should mostly shorten `learn`):

| | play (states/s) | learn (s per ~8k states) | overall st/s | peak RSS |
|---|---|---|---|---|
| `small`, V1 recipe | 350-490 | ~8 | 230-290 | 1.2 GB |
| `medium`, defaults | 150-270 | 12-45 | 90-175 | 2.4 GB |

Medium rollout inference on that CPU: 22 ms for 48 states, 28 ms for 64 (2.3k
states/s). Inference is not the bottleneck there: Showdown and poke-env are.

**Value vs policy gradients.** Policy and value share the transformer. For `medium`,
at `--vf-coef` 0.5 (before the value loss was normalized) the value loss sent the trunk
~5× the gradient norm of the policy loss (either value head), and on a 16-thread laptop
with a T1200 a 24 h run stalled around 43% vs the heuristic with `--lr 2e-4`. With
`--vf-coef 0.1 --lr 1e-4` the same run went from 43% to about 55% within ~150
iterations, then plateaued, still below `small` with the V1 recipe (about 61% over
2,000 battles). That run changed ~8 things at once and compared by hours, not states:
see [Ablation](#ablation). Also avoid starting PPO with a miscalibrated value head: a
distillation teacher whose value is flat makes the value gradient 20-50× the policy's.

Memory: with the dataset in shards on disk, it no longer limits anything. A medium
PPO run peaks around 2.4 GB for Python, plus Showdown.

## minipoke-v2 (encoder v2)

`--encoder-version 2` builds a model that reads `encoder_v2.py`'s observation: V1's six
arrays, unchanged (`encoder.py` is still V1's), then the random battle set priors, and
whatever the flags below add. Every flag defaults to the V1 architecture, so V1
checkpoints, exports and `make_fixtures.py` keep working. Architecture flags only apply
to a new model (`imitate`, `distill`, `ppo` without `--init`/`--resume`).

| Flag | `ModelConfig` | Needs v2 | What it adds |
|---|---|---|---|
| `--encoder-version 2` | `encoder_version` | | set priors: candidate moves, ability / item / tera weights |
| `--cand-moves K` | `cand_moves` (8) | yes | candidate move slots per opponent Pokémon |
| `--sets PATH` | (checkpoint) | yes | the sets file or Showdown clone (default `--showdown-cwd`) |
| `--move-tokens` | `move_tokens` | no | one token per (Pokémon, move slot); contextualized move head |
| `--history` | `history` | yes | last moves, switches, protects, choice-lock flag (field token) |
| `--history-tokens K` | `history_tokens` (0) | yes | the last K battle events as tokens (replaces `--history`) |
| `--matchup-tokens` | `matchup_tokens` | yes | 12 damage-calc matchup tokens, injected after layer n_layers // 2 |
| `--typed-ffn` | `typed_ffn` | no | one feed-forward block per token type |
| `--aux-action-coef`, `--aux-set-coef` (ppo) | `aux_heads` | no | training-only heads: opponent's action, opponent sets |

### Set priors

`setpriors.py` loads Showdown's gen 9 random battle sets: `data/random-battles/gen9/sets.json`
in the clone (it was `data/random-sets.json` from Dec 2022 to May 2024, and had no
abilities then). Each species has a few roles, each with a movepool, abilities and tera
types. **The gen 9 file has no items** (Showdown's `teams.ts` picks them in code), so
the item prior is empty with it. For each revealed opponent Pokémon:

- **Species**: its id, else its dex `battleOnly` form(s), then `baseSpecies`
  (Palafin-Hero → palafin).
- **Evidence**: revealed moves, its revealed base ability, revealed item, and its tera
  type once terastallized. Evidence outside the species' whole pool says nothing about
  its roles (Struggle, moves called by Sleep Talk, Trace, Illusion...) and is ignored.
  A role is consistent when it contains every remaining piece. No consistent role →
  every role of the species ("fallback").
- **Candidates**: moves of the consistent roles not revealed yet, ranked by the fraction
  of consistent roles containing them (descending), ties by move id (ascending), the
  first K = 8 kept. They go through the same move embedding and features as known
  moves (V1's move features with the candidate's owner and our active as target, full
  pp), plus a known/candidate flag and that fraction.
- **Abilities, items, tera types**: weight of x = mean over consistent roles of
  [x in role] / |role's x| (a revealed one: 1), as a weighted embedding sum (abilities,
  items) and a weighted multi-hot over V1's type order (tera).
- **Choice item possible**: a revealed Choice item, or an unknown item, no revealed
  status move other than Trick / Switcheroo / Healing Wish, and a consistent role that
  can be dealt a set without status moves (as `teams.ts` hands out Choice items).
- Unrevealed opponent slots stay "unknown" placeholders.

Checkpoints store the priors they were trained with (Showdown's sets change over
time), and exports write them to `set_priors.json`.

### Observation (inputs after V1's six)

| Array | Shape | |
|---|---|---|
| `cand_ids` | int64 [6, K] | candidate move ids per opponent slot (rows = V1's slots 6-11), PAD = none |
| `cand_feats` | f32 [6, K, 40] | V1's 39 move features + fraction |
| `prior_ids` | int64 [6, 2, 4] | row 0: ability ids, row 1: item ids (V1 hashing), by weight then id |
| `prior_w` | f32 [6, 2, 4] | their weights |
| `prior_feats` | f32 [6, 24] | tera weights (20 types), has sets, n consistent / n roles, fallback, Choice possible |
| `hist_ids` (`--history`) | int64 [2] | last move used by our active, by theirs (poke-env `Pokemon.last_move`) |
| `hist_feats` (`--history`) | f32 [8] | turns since their / our active came in, they / we switched by choice last turn, their / our protect counter, they chose the same move twice in a row, that and a Choice item possible |
| `event_ids` (`--history-tokens K`) | int64 [K, 2] | move id (PAD for an entrance), species id of the last K events, oldest first |
| `event_feats` (`--history-tokens K`) | f32 [K, 14] | present, ours, move, entrance, dragged, called/locked, crit, miss, super effective, resisted, immune, failed, KO, turns ago |
| `matchup_feats` (`--matchup-tokens`) | f32 [12, 16] | rows 0-5: our mon i vs their active, 6-11: our active vs their mon j; expected damage fraction of each known move both ways, max each way, KO flags, move order |

History reads poke-env's state (`last_move`, `_active_turns`, `protect_counter`) and
the battle's protocol log (`Battle._replay_data`, every parsed line but `|request|`)
for switches and chosen moves, so the encoder stays a function of the battle: re-encoding
recorded fixtures from their lines reproduces every array. Encoding costs about 0.38 ms
per state (V1: 0.18 ms), 0.54 ms with every option.

The damage estimate (`damage.py`) is deterministic and simplified on purpose: random
battle spreads for the opponent's stats (84 EVs, 31 IVs, neutral), STAB with tera,
poke-env's type effectiveness, burn, sun/rain, screens, always-crit moves, the mean roll,
mean hits, fixed-damage moves; no other ability or item.

### Tokens

    [actor, critic, field, 6 ours, 6 theirs,
     move tokens: 12 × 4 known (slot-major, V1 move order), 6 × K candidates   (--move-tokens)
     event tokens: K, oldest first                                            (--history-tokens)]
    + after the first n_layers // 2 layers: 12 matchup tokens                 (--matchup-tokens)

Move tokens carry learned embeddings of the owning Pokémon slot and of known/candidate,
event tokens of their position in the window, matchup tokens of their pair. Every token
after the critic is a state token: state tokens attend to each other, actor and critic
to the state and themselves (V1's mask). Empty move, event and matchup slots are masked
out as keys (a dense per-sample bool mask, `[B, 1, T, T]`, for SDPA: runs on MPS and
exports to ONNX). With move tokens, the move head reads our active's 4 move tokens
**after** the transformer, and the Pokémon tokens no longer concatenate their move
vectors. The switch head is unchanged. With `--typed-ffn`, actor/critic, field,
Pokémon, moves, events and matchups each get their own feed-forward block per layer.

Training cost of the `small` variants (forward + backward of 512 states, M-series laptop):

| | V1 | v2 | V1 + move tokens | v2 + move tokens |
|---|---|---|---|---|
| CPU | 88 ms | 120 ms | 269 ms | 504 ms |
| MPS | 58 ms | 72 ms | 174 ms | 342 ms |
| inference, 64 states, CPU | 4.0 ms | 5.6 ms | 12.3 ms | 24.8 ms |

With v2 and K = 8 the move tokens make 111 tokens instead of 15. Rollouts are still
bound by Showdown and poke-env (a few hundred states per second).

### encoder_spec.json additions

A V1 model's spec is unchanged, byte for byte. A v2 model's spec adds:
- `"encoder_version": 2` (first key) and `"inputs"`: every ONNX input in order, with its
  shape and dtype;
- `"model"`: the config fields that shape the observation (`encoder_version`,
  `cand_moves`, `history`, `history_tokens`, `matchup_tokens`);
- `"set_priors"`: the file (`set_priors.json`), its sha256, the sha256 of the Showdown
  sets file it came from, K, the species lookup, evidence, consistency, ranking,
  tie-breaking, weighting and Choice rules;
- `"history"`, `"history_tokens"`, `"matchup_tokens"` (when used): every rule, including
  what the C# tracker must keep beyond poke-env's state (the protocol log) and the
  damage estimate's formula;
- `"orderings"`: also `prior_feats`, `cand_feats`, `hist_feats`, `event_feats`,
  `matchup_feats`.

### Auxiliary heads, rewards, exploration (from [Jaxcalibur](https://jaxcalibur.github.io/))

- `--aux-action-coef C`, `--aux-set-coef C` (ppo, default 0) add training-only heads
  (never exported: `forward()` does not run them). One predicts the opponent's action in
  the same turn, in our view's 14-way space (switch target: its revealed slot or the
  next unrevealed one; move: its revealed index or the next one; +4 with tera), masked
  by the opponent's mapped legal actions. The others predict, per revealed opponent
  Pokémon token, its item, ability, tera type (classification) and moves (multi-label).
  Labels come from the other seat of mirror and pool games (`aux_labels.py`: decisions
  matched by battle tag, role and turn; true sets from that seat's own request).
  metrics.csv logs each loss and accuracy and the share of labelled steps.
- `--reward winloss`: 1 for a win, 0 otherwise, no faint shaping, gamma 1.
- `--uniform-kl-coef C`: adds C × KL(uniform over the legal actions || policy).

## Elo league table

`elo.py` rates the learner, every pool snapshot and the scripted players. The scripted
players are anchors with fixed ratings, so ratings compare across runs:

| Anchor | Rating |
|---|---|
| `random` (RandomPlayer) | 0 |
| `maxpower` (MaxBasePowerPlayer) | 410 |
| `heuristic` (SimpleHeuristicsPlayer) | 780 |

They were fitted with `python train.py elo --scripted heuristic maxpower random --anchors random=0`
over 2,500 battles per pair on a local Showdown (Oct 2026): the heuristic beat
maxpower 89.0% and random 99.1%, maxpower beat random 91.2% (777 and 410 by maximum
likelihood). Refit them after a Showdown update before comparing with older runs, and
pass `--elo-anchors random=0,maxpower=...,heuristic=...`.

During PPO the table is updated after every iteration from every game against the pool
and the heuristic, training and evaluation (mirror games carry no information): one Elo
update per game at the batch's average score, k = `--elo-k` 16. The learner starts at the
heuristic's rating, a new snapshot at the learner's. It is saved to `runs/<run>/elo.json`
and in checkpoints, and `elo` in metrics.csv is the learner's rating.

`python train.py elo --ckpts A B ... --scripted heuristic maxpower random --battles 200`
plays a round-robin (models greedy, each with its own encoder) and fits Bradley-Terry
ratings by maximum likelihood with the anchors that take part fixed (`--out` writes them).

## Ablation

Compare runs **at matched training states** (`--states`), not hours, and change one
thing at a time. Read `best.pt` (confirmed over 400 battles) and the final checkpoint
with `eval --battles 1000` (95% interval about ±3%), plus one `elo` round-robin of all
the runs' `best.pt`. Plot with `python plot_metrics.py runs/a runs/b ...` (x = states).
Below, `S=5000000` (about 5.5 h for small, 9 h for medium at the speeds above) and
`--seed 1` everywhere; `--concurrency` is fixed to 32 since it does not change learning.

### 1. Size × recipe at matched states (2×2)

Every flag that differs between the recipes, spelled out:

| | V1 recipe | medium recipe |
|---|---|---|
| value head (`imitate`) | `--value-bins 1` | `--value-bins 51` |
| BC value fit (`imitate`) | `--bc-value-coef 0` | `--bc-value-coef 0.5` |
| `ppo` | `--lr 2e-4 --warmup-iters 0 --lr-min-ratio 1.0 --ent-start 0.01 --ent-end 0.01 --mirror-share 1 --pool-share 0 --heuristic-share 0 --kl-stop last --battles-per-iter 128` | `--lr 1e-4 --warmup-iters 10 --lr-min-ratio 0.1 --ent-start 0.01 --ent-end 0.003 --mirror-share 0.7 --pool-share 0.2 --heuristic-share 0.1 --pool-sampling pfsp --kl-stop mean --battles-per-iter 192` |
| shared | `--vf-coef 0.5 --value-ce-norm log-bins` | same |

```bash
S=5000000
V1R="--lr 2e-4 --warmup-iters 0 --lr-min-ratio 1.0 --ent-start 0.01 --ent-end 0.01 \
     --mirror-share 1 --pool-share 0 --heuristic-share 0 --kl-stop last --battles-per-iter 128"
MR="--lr 1e-4 --warmup-iters 10 --lr-min-ratio 0.1 --ent-start 0.01 --ent-end 0.003 \
    --mirror-share 0.7 --pool-share 0.2 --heuristic-share 0.1 --pool-sampling pfsp \
    --kl-stop mean --battles-per-iter 192"
COMMON="--states $S --seed 1 --concurrency 32 --vf-coef 0.5 --value-ce-norm log-bins"

# One BC dataset (V1 observations) for the four BC models
python train.py imitate --size small --value-bins 1 --bc-value-coef 0 --battles 10000 --epochs 6 \
    --data data/bc-10k --ckpt-dir checkpoints/small-v1r --seed 1
python train.py imitate --size small --value-bins 51 --bc-value-coef 0.5 --epochs 6 \
    --data data/bc-10k --ckpt-dir checkpoints/small-mr --seed 1
python train.py imitate --size medium --value-bins 1 --bc-value-coef 0 --epochs 6 \
    --data data/bc-10k --ckpt-dir checkpoints/medium-v1r --seed 1
python train.py imitate --size medium --value-bins 51 --bc-value-coef 0.5 --epochs 6 \
    --data data/bc-10k --ckpt-dir checkpoints/medium-mr --seed 1

python train.py ppo --init checkpoints/small-v1r/bc.pt  --ckpt-dir checkpoints/small-v1r  --run small-v1r  $COMMON $V1R
python train.py ppo --init checkpoints/small-mr/bc.pt   --ckpt-dir checkpoints/small-mr   --run small-mr   $COMMON $MR
python train.py ppo --init checkpoints/medium-v1r/bc.pt --ckpt-dir checkpoints/medium-v1r --run medium-v1r $COMMON $V1R
python train.py ppo --init checkpoints/medium-mr/bc.pt  --ckpt-dir checkpoints/medium-mr  --run medium-mr  $COMMON $MR

for c in small-v1r small-mr medium-v1r medium-mr; do
  python train.py eval --ckpt checkpoints/$c/best.pt --battles 1000
done
python train.py elo --ckpts checkpoints/{small-v1r,small-mr,medium-v1r,medium-mr}/best.pt --battles 400
```

The medium recipe of the 8 h run that reached ~55% was the raw value loss, last-minibatch
KL stop and uniform pool: add `--value-ce-norm none --vf-coef 0.1 --kl-stop last
--pool-sampling uniform` to `MR` to run that one instead.

### 2. minipoke-v2 features, one at a time, on small

Each student is distilled from the same V1 checkpoint (here `checkpoints/v1-small.pt`,
MiniPokeV1_1M's), then trained with the V1 recipe (small's defaults) at matched states.
B0 is the control: a V1 student through the same pipeline.

```bash
S=5000000; T=checkpoints/v1-small.pt
D="--size small --battles 10000 --epochs 6 --seed 1"      # distill
P="--states $S --seed 1 --concurrency 32"                  # ppo, V1 recipe

# B0 control (V1 encoder)                            B1 set priors (v2)
python train.py distill --teacher $T $D --data data/d-v1 --ckpt-dir checkpoints/b0
python train.py distill --teacher $T $D --encoder-version 2 --data data/d-v2 --ckpt-dir checkpoints/b1
# B2 = B1 + move tokens (same data)                  B3 = B1 + history features
python train.py distill --teacher $T $D --encoder-version 2 --move-tokens --data data/d-v2 --ckpt-dir checkpoints/b2
python train.py distill --teacher $T $D --encoder-version 2 --history --data data/d-v2-hist --ckpt-dir checkpoints/b3

for b in b0 b1 b2 b3; do
  python train.py ppo --init checkpoints/$b/distill.pt --ckpt-dir checkpoints/$b --run $b $P
done
```

Compare B1 with B0 (priors), B2 with B1 (move tokens), B3 with B1 (history).

### 3. Jaxcalibur ideas, each alone on small, from V1 distillation

```bash
# D1 auxiliary heads (V1 encoder: compare with B0)
python train.py ppo --init checkpoints/b0/distill.pt --ckpt-dir checkpoints/d1 --run d1 $P \
    --aux-action-coef 0.1 --aux-set-coef 0.1
# D2 matchup tokens (needs v2: compare with B1)
python train.py distill --teacher $T $D --encoder-version 2 --matchup-tokens --data data/d-v2-match --ckpt-dir checkpoints/d2
python train.py ppo --init checkpoints/d2/distill.pt --ckpt-dir checkpoints/d2 --run d2 $P
# D3 history event tokens (needs v2: compare with B1 and B3)
python train.py distill --teacher $T $D --encoder-version 2 --history-tokens 16 --data data/d-v2-events --ckpt-dir checkpoints/d3
python train.py ppo --init checkpoints/d3/distill.pt --ckpt-dir checkpoints/d3 --run d3 $P
# D4 typed feed-forward blocks (V1 encoder: compare with B0; same data as B0)
python train.py distill --teacher $T $D --typed-ffn --data data/d-v1 --ckpt-dir checkpoints/d4
python train.py ppo --init checkpoints/d4/distill.pt --ckpt-dir checkpoints/d4 --run d4 $P
# D5 win/loss reward, and the uniform-KL term, separately (compare with B0)
python train.py ppo --init checkpoints/b0/distill.pt --ckpt-dir checkpoints/d5-winloss --run d5-winloss $P --reward winloss
python train.py ppo --init checkpoints/b0/distill.pt --ckpt-dir checkpoints/d5-ukl --run d5-ukl $P --uniform-kl-coef 0.01
```

The coefficients are starting points (aux 0.1 each; uniform KL 0.01, the entropy
coefficient's scale): sweep them only for ideas that look promising.

### Suggested experiment order

1. **Re-evaluate what exists**, 1000 battles each, and head to head:
   `eval --ckpt checkpoints/v1-small.pt`, `eval --ckpt <medium>/best.pt`,
   `eval --ckpt <medium>/best.pt --vs-ckpt checkpoints/v1-small.pt`, plus one `elo`
   round-robin with the scripted players. 62% vs 55% may not survive 1000 battles.
2. **The 2×2 at matched states** (section 1): is medium worse because of its size or its
   recipe?
3. **minipoke-v2 features one at a time on small** (section 2), each initialized by
   distilling V1: set priors, then move tokens, then history. Then the Jaxcalibur ideas
   (section 3), each alone.
4. **Only then medium + v2**, with what helped on small (distill V1 into
   `--size medium --encoder-version 2 ...`, then the recipe that won in step 2).

## Files

- `encoder.py` turns a poke-env `Battle` into tensors (12 Pokémon tokens, moves,
  field) plus the 14-action mask: encoder V1, unchanged. Actions map onto poke-env's
  `SinglesEnv` action space, so orders are always built by poke-env itself.
- `encoder_v2.py` (minipoke-v2): V1's arrays plus set priors, history, events and
  matchups, by flag; `encoder_for(cfg, priors)` picks a model's encoder.
- `setpriors.py` (random battle set priors), `history.py` (protocol-log history and
  events), `damage.py` (the matchup damage estimate).
- `model.py` holds `MiniPokeTransformer`, `ModelConfig` and the `small` / `medium` / `wide` presets.
- `players.py` has `InferenceBatcher` (async batching, with its model's encoder),
  `RLPlayer` (records trajectories with win ±1 and faint ±0.1 rewards, as in ps-ppo, or
  `--reward winloss`), and the recording teachers `TeacherPlayer` (heuristic) and
  `DistillPlayer` (a trained model, any student encoder).
- `ppo.py` has GAE, two-hot value targets, the budget, LR/entropy schedules, the PPO
  update (KL stop, uniform KL, auxiliary losses) and the distillation / behavior-cloning loop.
- `dataset.py` holds the sharded on-disk dataset and its streaming loader.
- `rollout.py` decides who plays whom in an iteration, runs the watchdog, and has the
  scripted players, evaluation and head-to-head matches.
- `league.py` holds the opponent pool (uniform or pfsp). `elo.py` the Elo table and the
  round-robin fit. `aux_labels.py` labels the auxiliary heads from the other seat.
  `checkpoint.py` saves and restores the full training state. `runtime.py` has the
  Showdown launcher, signals, metrics CSV, checkpoint retention, inference benchmark and
  MPS fallback detection.
- `train.py` is the CLI. `plot_metrics.py` plots runs.
- `export_onnx.py` exports a checkpoint to ONNX, with `encoder_spec.json`
  (vocabularies, item hashing, one-hot orderings, feature sizes, and the v2 rules) and,
  for v2, `set_priors.json`.
- `tests/` (pytest; `pip install -r requirements-dev.txt`) covers the export contract (V1
  byte-identical, every v2 flag's export and ONNX parity), the encoders on offline
  poke-env battles (priors, history, events, matchups, action mapping), the model (masks,
  move tokens, typed FFN, MPS), GAE and two-hot targets, the PPO loop end to end with
  faked rollouts (budgets, confirmed best, pool, Elo, aux), distillation across
  encoders, resume, the pool, the dataset and clean stops.
- `make_fixtures.py` records battles for ElsaMina's parity tests (V1, or v2 from a v2 spec).

## Using a model in ElsaMina

Each exported model is named after its **version** (the `encoder.py` it was trained
with) and its size in parameters: `MiniPokeV1_1M`, `MiniPokeV1_4M`, later `MiniPokeV2_...`.
Models of one version share their C# encoder. ElsaMina keeps every model side by side,
so they can be compared with `-strategy minipokevN-size` (`minipokev1-1m`, `minipokev1-4m`;
`-strategy minipoke` picks the latest one).

The C# side reproduces what the model sees in training:

| Python | C# |
|---|---|
| poke-env's battle tracking | `src/ElsaMina.Battles/Tracking` (shared by every version) |
| `encoder.py`: slots, ids, item hashing, action mask | `Strategies/MiniPoke/MiniPokeEncoder.cs` (shared) |
| `encoder.py`: feature vectors | `Strategies/MiniPokeVN/MiniPokeVNEncoder.cs` (one per version) |
| `export_onnx.py` outputs | `src/ElsaMina.Battles/Models/MiniPokeVN_SIZE/` (one per model) |
| ONNX inference, action → `/choose` | `Strategies/MiniPoke/` (shared) |

Any difference between the two encoders is silent: the model still plays legal
moves, just badly. The parity fixtures catch it.

### Replacing a released model

`encoder.py` and a V1 model's `encoder_spec.json` have not changed, so a V1 model trained
here (any size) runs with V1's C# encoder as is. To replace a released model with one of
about the same size (`MiniPokeV1_4M` here), only `minipoke.onnx` changes:

```bash
python export_onnx.py --ckpt checkpoints/best.pt --out export/
cp export/minipoke.onnx ../../src/ElsaMina.Battles/Models/MiniPokeV1_4M/minipoke.onnx
git diff --stat ../../src/ElsaMina.Battles/Models/MiniPokeV1_4M/encoder_spec.json  # must be empty
python make_fixtures.py --version 1_4M --battles 20    # Showdown running
```

The last step is required: the parity fixtures also store the logits of the model
they were recorded with (`MiniPokeV1ParityTest` compares them with ElsaMina's
onnxruntime), so they must be recorded again with the new `minipoke.onnx`. The
encoding and action checks stay the same. To keep the old model available
side by side instead, release it as a new model (below), reusing V1's encoder.

### Releasing a new model N_SIZE

`N_SIZE` is the version and the size, `1_4M` for minipoke v1 with ~4M parameters,
`2_1M` for a ~1M minipoke-v2 model (strategy `minipokev2-1m`).

1. Export the checkpoint into its model directory:
   ```bash
   python export_onnx.py --ckpt checkpoints/best.pt --out ../../src/ElsaMina.Battles/Models/MiniPokeVN_SIZE
   ```
2. Record parity fixtures with the encoder this model was trained with
   (Showdown running):
   ```bash
   python make_fixtures.py --version N_SIZE --battles 20
   ```
3. On the C# side:
   - add `MiniPokeVN_SIZE` to `BattleDecisionStrategy`;
   - add `RegisterMiniPoke<...>(builder, BattleDecisionStrategy.MiniPokeVN_SIZE);` to `BattlesModule`.
     If `encoder.py`'s features did not change, reuse the previous version's encoder.
     If they did, this is a new version: copy it to `Strategies/MiniPokeVN/MiniPokeVNEncoder.cs`
     and port the changes;
   - add the model to `MODELS` in its version's parity test. For a new version, copy
     `test/.../Battles/Strategies/MiniPokeV1/MiniPokeV1ParityTest.cs` (and its
     `FeatureNames` if the features changed) to `MiniPokeVN`.
4. Run the tests. `MiniPokeVNParityTest` must pass for the model: every array equal
   to what the Python encoder produced, every action mapped to the order poke-env sent,
   and the same logits in Python's onnxruntime and ElsaMina's.

**minipoke-v2 (not ported yet).** Its C# encoder keeps V1's arrays and adds the ones its
`encoder_spec.json` lists under `"inputs"`, following the spec's rules: it loads the
model's `set_priors.json` (the priors are the model's, not Showdown's current file), and
with history, events or matchups it needs the battle's protocol lines in order (the
tracker must keep them), poke-env's `last_move` / `_active_turns` / `protect_counter`, and
`damage.py`'s estimate. The v2 fixtures carry `"encoder_version": 2` and every input.

Older models keep working as long as their encoder stays as it was. Their
fixtures were recorded with their own encoder: tag the commit of each
release (`git tag minipoke-v1-4m`) to regenerate them later.

Upgrading poke-env, or changing what an encoder reads from it, means checking
the C# tracker against new fixtures too.

## What to expect

This is a proof of concept, not a reproduction of the 1900+ result. The
original's turn history matters for hidden information (inferring sets,
predicting switches), and so do its size and its 250M training states. A
realistic goal for this model on a laptop is to clearly beat
`SimpleHeuristicsPlayer` after BC plus a few hours of PPO.

minipoke-v2 covers part of that gap cheaply: set priors for hidden information, a few
history features or the last K events instead of a KV cache over 64 turns, and an
opponent pool favouring the snapshots the learner loses to. Whether each helps is what
the [ablation](#ablation) is for.

## Porting the original ps-ppo model to MPS instead

If you want the full 16M model on the Mac rather than this one:
1. In `ppo_core.py`, replace the `flex_attention(...)` call in `FlexEncoderLayer`
   with `F.scaled_dot_product_attention(q, k, v, attn_mask=dense_bool_mask)`.
   Build the dense mask from the same rules as the block mask; sequences are
   short, so a dense mask is cheap.
2. Drop the `logit_soft_cap` score_mod, or apply `tanh` soft-capping by
   writing out attention manually (softmax(QKᵀ) with the cap).
3. In `config.py`, add `"mps" if torch.backends.mps.is_available()` to the
   device selection, and disable any `torch.compile`/bf16 paths that fail on MPS.
4. Lower `target_concurrent_battles` (800 → ~100-200) and shrink
   `minibatch_size`/`batch_seq_len`, raising `grad_accum_steps` to compensate.
