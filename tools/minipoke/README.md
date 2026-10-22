# minipoke: a small PokeTransformer for an M3 Mac

A proof of concept based on [Nebraskinator/ps-ppo](https://github.com/Nebraskinator/ps-ppo),
shrunk to run on Apple Silicon with [poke-env](https://github.com/hsahovic/poke-env)
and a **single** local Showdown server. ElsaMina plays with the exported models
(`-strategy minipokev1`, see [Using a model in ElsaMina](#using-a-model-in-elsamina)).

| | ps-ppo | minipoke `small` (V1) | minipoke `medium` |
|---|---|---|---|
| Parameters | ~16M | 0.91M | 3.85M |
| d_model / layers / heads | 512 / 3 / 8 | 128 / 3 / 4 | 256 / 4 / 8 |
| Embeddings (species / item / ability / move) | | 32 / 16 / 16 / 32 | 64 / 32 / 32 / 64 |
| Attention | `flex_attention` (Triton, CUDA-only) | `scaled_dot_product_attention` + boolean mask (MPS ok) | same |
| Turn history | KV cache, 64 turns | none (current state only) | none |
| Policy head | linear on actor token | pointer heads: switch logits from team tokens, move logits from the active Pokémon's move vectors | same |
| Value head | 51-bin distributional | scalar | 51-bin two-hot over [-1.6, 1.6] |
| Environment | many Showdown servers + Ray workers | one server, poke-env, asyncio, batched inference | same |
| Training | BC → PPO | BC → mirror self-play PPO | BC (or distillation) → PPO with an opponent pool, schedules, resumable |

Both sizes export the **same ONNX interface** (inputs, dtypes, shapes, `logits [B,14]`,
`value [B]`) and the same `encoder_spec.json`, byte for byte: the distributional head
returns its expected value, so ElsaMina cannot tell them apart.
`tests/test_export_contract.py` checks this.

## Setup (macOS, Apple Silicon)

Everything runs from this directory. The virtual environment, checkpoints,
exports and the Showdown clone stay here, ignored by git.

```bash
cd tools/minipoke

# 1. Python env
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt          # torch from PyPI includes MPS on macOS

# 2. One local Showdown server (current Showdown needs Node 22+)
git clone https://github.com/smogon/pokemon-showdown.git
cd pokemon-showdown && npm install && cp config/config-example.js config/config.js
node pokemon-showdown start --no-security   # leave running, port 8000
```

If you push concurrency high, raise `exports.subprocesses` in Showdown's
`config/config.js` so battles are simulated across several CPU cores. That
is still one server, just with more simulator processes.

poke-env is pinned on purpose: ElsaMina's battle tracker is a port of that exact
version (see below).

## Run

```bash
# Phase 1: imitate SimpleHeuristicsPlayer
python train.py imitate --size medium --battles 3000 --epochs 8
#   or distil an already trained model (its full action distribution and value)
python train.py distill --teacher checkpoints/ppo_latest.pt --size medium --battles 3000

# Phase 2: PPO from that checkpoint, with a time budget
python train.py ppo --init checkpoints/bc.pt --hours 24 \
    --showdown-cmd "node pokemon-showdown start --no-security"

# Stop it (Ctrl-C, SIGTERM, or `touch runs/<run>/STOP`), then continue later
python train.py ppo --resume checkpoints/ppo_latest.pt

# Evaluate any checkpoint, plot a run, export
python train.py eval --ckpt checkpoints/best.pt --opponent heuristic --battles 200
python plot_metrics.py runs/<run>           # -> runs/<run>/metrics.png
python export_onnx.py --ckpt checkpoints/best.pt --out export/
```

### Sizes and their defaults

`--size` picks the architecture of a new model and the defaults of every option
not given on the command line. `--init` and `--resume` always keep the
architecture stored in the checkpoint (older checkpoints load as `small`).

| | `small` (the V1 recipe, unchanged) | `medium` |
|---|---|---|
| Value head | scalar, MSE | 51 bins, two-hot cross-entropy (`--value-bins`) |
| BC value fit (`--bc-value-coef`) | 0 (policy only) | 0.5, to the battles' discounted returns |
| LR (`--lr`, `--warmup-iters`, `--lr-min-ratio`) | 2e-4 constant | 1e-4 peak, 10-iteration warmup, cosine to 10% |
| Value loss weight (`--vf-coef`) | 0.5 | 0.1 |
| Entropy coef. (`--ent-start`, `--ent-end`) | 0.01 constant | 0.01 → 0.003, linearly |
| Opponents (`--mirror-share`, `--pool-share`, `--heuristic-share`) | 100% mirror | 70% mirror / 20% pool / 10% heuristic |
| `--concurrency` / `--battles-per-iter` | 32 / 128 | 48 / 192 |
| Iteration budget | 300 (when `--hours` is not given) | same |

Every other PPO hyperparameter (`PPOConfig`: gamma 0.995, λ 0.95, clip 0.2,
3 epochs, minibatch 1024, target KL 0.03) is shared.

### Distillation and the BC dataset

`distill` plays teacher-vs-teacher battles (the teacher samples its own policy, and
plays a uniformly random legal move with probability `--epsilon`, 0.05). It records the
teacher's whole softmax and its value, then trains the student on the cross-entropy to
that distribution (KL to the teacher plus a constant). It also trains
`--vf-coef` × value loss toward the teacher's value (`--value-target return` uses the
battle's real returns instead). Validation reports the KL and the argmax agreement.

`imitate` and `distill` write their samples to `data/<name>/` as compressed `.npz`
shards: float16 features, int16 ids, a bit-packed mask. A sample is about
110–130 bytes on disk, against ~12 KB as a float32 numpy dict. That's ~5 KB per battle,
so 10k battles take about 50 MB. Training streams the shards (a few decoded at a time,
shuffled together), so RAM stays flat whatever the dataset size. Use `--data DIR` to
pick the directory; a directory that already holds a dataset is reused without
playing (e.g. to retrain with other epochs).

### Long runs

- **Budget.** `--hours H` and `--iterations N` are *totals for the run*. Training stops
  at whichever comes first. Resuming counts the time already spent. The cosine LR
  and the entropy annealing follow the progress `max(iterations / N, hours / H)`.
  Raising `--hours` on `--resume` extends the run, and the schedules stretch to match.
- **Stopping and resuming.** The first Ctrl-C / SIGTERM / `runs/<run>/STOP` finishes the
  current iteration, saves, and exports. A second Ctrl-C quits at once.
  `checkpoints/ppo_latest.pt` is rewritten (atomically) after every iteration. It
  holds the model, optimizer, schedule progress, iteration, states, RNG states,
  pool manifest, best score and every option, so `--resume` continues exactly
  (any option given again on the command line overrides the saved one).
- **Opponent pool.** Every `--snapshot-every` iterations the policy is saved to
  `checkpoints/pool/` (cap `--pool-cap` 20). The starting policy is the first snapshot.
  When the pool is full, the oldest and the 5 newest snapshots stay, and among the
  others the one leaving the smallest gap goes, which keeps a spread of ages. Each
  iteration samples `--pool-slots` (2) snapshots. Each slot has its own inference
  model and batcher, while all learner seats share one. Only the learner's side of
  pool and heuristic battles is trained on.
- **Evaluation.** Every `--eval-every` iterations: greedy win rate against
  `SimpleHeuristicsPlayer` (`--eval-battles` 200) and against the 3 newest snapshots
  (`--eval-pool-battles` 100 each). `checkpoints/best.pt` keeps the best heuristic win
  rate, and only counts evaluations with at least `--min-eval-battles` (150) finished
  battles. At the end, `best.pt` (or the latest checkpoint if there is none) is
  exported to `export/`.
- **Robustness.** A battle that raises in `choose_move` plays a random move and the
  error is logged. If no battle finishes for `--stall-timeout` seconds (180), or an
  iteration takes over 4× the recent median, the iteration is cancelled. Its finished
  battles are still used, the players reconnect with fresh websockets, and the run
  goes on. With `--showdown-cmd`, the server is started if it is down and restarted
  if it dies (its output goes to `runs/<run>/showdown.log`). After 5 failed iterations
  in a row the run saves and stops.
- **Checkpoints on disk.** `ppo_latest.pt`, `best.pt`, the last 5 `ppo_NNNNN.pt`
  (every `--save-every` iterations) and every 50th. A medium checkpoint with Adam
  state is ~45 MB, and a pool snapshot ~15 MB.
- **Logs.** `runs/<run>/train.log`, plus one row per iteration in
  `runs/<run>/metrics.csv`: states, st/s, losses, entropy, KL, clip fraction,
  explained variance, LR, entropy coefficient, training win rates by opponent, eval
  win rates, pool size, stalls and RSS.

How it runs:
- **Training** (BC and PPO updates) uses `--device auto`: MPS on an M3, CUDA on a
  Linux GPU box. Training on MPS keeps `PYTORCH_ENABLE_MPS_FALLBACK=1`, but every
  operator that falls back to the CPU is logged as `[mps-fallback]`. A forward and
  backward self-check at startup reports it before the run gets going.
- **Rollout inference** uses `--infer-device auto` for `ppo`: it times a forward pass on
  the CPU and on the training device at rollout batch sizes and keeps the faster one
  (`[bench]` lines in the log). Batches are capped at `--max-infer-batch` 64: on a CPU,
  the medium model's throughput peaks there and drops for larger batches.
- `--concurrency` sets concurrent battles per seat pair. With `medium`, the learner
  plays about `concurrency` battles at a time, split across the opponent types.

### A long run on the M3 (16 GB)

```bash
python train.py imitate --size medium --battles 10000 --epochs 6      # ~50 MB of data
python train.py ppo --init checkpoints/bc.pt --hours 24 \
    --showdown-cmd "node pokemon-showdown start --no-security"
python plot_metrics.py runs/<run>
```

Raise `exports.subprocesses` in Showdown's `config/config.js` (e.g. 6–8 simulator
processes): rollouts are mostly Showdown and poke-env time. Run a 20-minute
`ppo --hours 0.33` first and read `st/s` and the `play`/`learn` split. Total states ≈
st/s × 3600 × hours. At 150 st/s, 24 h is about 13M states.

Measured so far, on a 16-thread x86 laptop CPU with **no GPU**, training on the CPU
(not an M3; MPS should mostly shorten `learn`):

| | play (states/s) | learn (s per ~8k states) | overall st/s | peak RSS |
|---|---|---|---|---|
| `small`, V1 recipe | 350–490 | ~8 | 230–290 | 1.2 GB |
| `medium`, defaults | 150–270 | 12–45 | 90–175 | 2.4 GB |

Medium rollout inference on that CPU: 22 ms for 48 states, 28 ms for 64 (2.3k
states/s). Inference is not the bottleneck there: Showdown and poke-env are.

**Value vs policy gradients.** Policy and value share the transformer. For `medium`,
at `--vf-coef` 0.5 the value loss sends the trunk ~5× the gradient norm of the policy
loss (either value head), and on a 16-thread laptop with a T1200 a 24 h run stalled
around 43% vs the heuristic with `--lr 2e-4`. With `--vf-coef 0.1 --lr 1e-4` (now the
`medium` defaults) the same run went from 43% to about 55% within ~150 iterations,
then plateaued, still below `small` with the V1 recipe (about 61% over 2,000 battles).
Also avoid starting PPO with a miscalibrated value head: a distillation teacher whose
value is flat makes the value gradient 20–50× the policy's.

Memory: with the dataset in shards on disk, it no longer limits anything. A medium
PPO run peaks around 2.4 GB for Python, plus Showdown.

## Files

- `encoder.py` turns a poke-env `Battle` into tensors (12 Pokémon tokens, moves,
  field) plus the 14-action mask. Actions map onto poke-env's `SinglesEnv`
  action space, so orders are always built by poke-env itself.
- `model.py` holds `MiniPokeTransformer`, `ModelConfig` and the `small` / `medium` presets.
- `players.py` has `InferenceBatcher` (async batching), `RLPlayer` (records
  trajectories with win ±1 and faint ±0.1 rewards, as in ps-ppo), and the
  recording teachers `TeacherPlayer` (heuristic) and `DistillPlayer` (a trained model).
- `ppo.py` has GAE, two-hot value targets, the LR/entropy schedules, the PPO update
  and the distillation / behavior-cloning loop.
- `dataset.py` holds the sharded on-disk dataset and its streaming loader.
- `rollout.py` decides who plays whom in an iteration and runs the watchdog.
- `league.py` holds the opponent pool. `checkpoint.py` saves and restores the full
  training state. `runtime.py` has the Showdown launcher, signals, metrics CSV,
  checkpoint retention, inference benchmark and MPS fallback detection.
- `train.py` is the CLI. `plot_metrics.py` plots a run.
- `export_onnx.py` exports a checkpoint to ONNX, with `encoder_spec.json`
  (vocabularies, item hashing, one-hot orderings, feature sizes).
- `tests/` (pytest; `pip install -r requirements-dev.txt`) covers the export contract,
  GAE and two-hot targets, resume, the pool, the dataset and clean stops.
- `make_fixtures.py` records battles for ElsaMina's parity tests.

## Using a model in ElsaMina

Each exported model is a **version**: `MiniPokeV1`, `MiniPokeV2`... ElsaMina keeps
every version side by side, so they can be compared with `-strategy minipokevN`
(`-strategy minipoke` picks the latest one).

The C# side reproduces what the model sees in training:

| Python | C# |
|---|---|
| poke-env's battle tracking | `src/ElsaMina.Battles/Tracking` (shared by every version) |
| `encoder.py`: slots, ids, item hashing, action mask | `Strategies/MiniPoke/MiniPokeEncoder.cs` (shared) |
| `encoder.py`: feature vectors | `Strategies/MiniPokeVN/MiniPokeVNEncoder.cs` (one per version) |
| `export_onnx.py` outputs | `src/ElsaMina.Battles/Models/MiniPokeVN/` |
| ONNX inference, action → `/choose` | `Strategies/MiniPoke/` (shared) |

Any difference between the two encoders is silent: the model still plays legal
moves, just badly. The parity fixtures catch it.

### Deploying a retrained model in place of V1

`encoder.py` and `encoder_spec.json` have not changed, so a model trained here (small
or medium) runs with V1's C# encoder as is. Only `minipoke.onnx` changes:

```bash
python export_onnx.py --ckpt checkpoints/best.pt --out export/
cp export/minipoke.onnx ../../src/ElsaMina.Battles/Models/MiniPokeV1/minipoke.onnx
git diff --stat ../../src/ElsaMina.Battles/Models/MiniPokeV1/encoder_spec.json  # must be empty
python make_fixtures.py --version 1 --battles 20    # Showdown running
```

The last step is required: V1's parity fixtures also store the logits of the model
they were recorded with (`MiniPokeV1ParityTest` compares them with ElsaMina's
onnxruntime), so they must be recorded again with the new `minipoke.onnx`. The
encoding and action checks stay the same. To keep the old model available
side by side instead, release it as a new version (below), reusing V1's encoder.

### Releasing a new version N

1. Export the checkpoint into its model directory:
   ```bash
   python export_onnx.py --ckpt checkpoints/ppo_latest.pt --out ../../src/ElsaMina.Battles/Models/MiniPokeVN
   ```
2. Record parity fixtures with the `encoder.py` this model was trained with
   (Showdown running):
   ```bash
   python make_fixtures.py --version N --battles 20
   ```
3. On the C# side:
   - add `MiniPokeVN` to `BattleDecisionStrategy`;
   - add `RegisterMiniPoke<...>(builder, BattleDecisionStrategy.MiniPokeVN);` to `BattlesModule`.
     If `encoder.py`'s features did not change, reuse the previous version's encoder.
     If they did, copy it to `Strategies/MiniPokeVN/MiniPokeVNEncoder.cs` and port the changes;
   - copy `test/.../Battles/Strategies/MiniPokeV1/MiniPokeV1ParityTest.cs` (and its
     `FeatureNames` if the features changed) to `MiniPokeVN`.
4. Run the tests. `MiniPokeVNParityTest` must pass: every array equal to what
   `encoder.py` produced, every action mapped to the order poke-env sent, and the
   same logits in Python's onnxruntime and ElsaMina's.

Older versions keep working as long as their encoder stays as it was. Their
fixtures were recorded with their own `encoder.py`: tag the commit of each
release (`git tag minipoke-vN`) to regenerate them later.

Upgrading poke-env, or changing what `encoder.py` reads from it, means checking
the C# tracker against new fixtures too.

## What to expect

This is a proof of concept, not a reproduction of the 1900+ result. The
original's turn history matters for hidden information (inferring sets,
predicting switches), and so do its size and its 250M training states. A
realistic goal for this model on a laptop is to clearly beat
`SimpleHeuristicsPlayer` after BC plus a few hours of PPO.

Natural next steps, roughly in order of payoff:
1. **Turn history.** Add a small per-turn "field/event" token and attend over
   the last N turns. This is the original's biggest feature missing here (and it
   changes the encoder, so it is a new ElsaMina version).
2. **A stronger league.** Prioritise pool opponents the current policy loses to
   instead of sampling them uniformly.

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
4. Lower `target_concurrent_battles` (800 → ~100–200) and shrink
   `minibatch_size`/`batch_seq_len`, raising `grad_accum_steps` to compensate.
