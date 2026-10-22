# minipoke: a small PokeTransformer for an M3 Mac

A proof of concept based on [Nebraskinator/ps-ppo](https://github.com/Nebraskinator/ps-ppo),
shrunk to run on Apple Silicon with [poke-env](https://github.com/hsahovic/poke-env)
and a **single** local Showdown server. ElsaMina plays with the exported models
(`-strategy minipokev1-4m`, see [Using a model in ElsaMina](#using-a-model-in-elsamina)).

| | ps-ppo | minipoke |
|---|---|---|
| Parameters | ~16M | ~4M |
| d_model / layers / heads | 512 / 3 / 8 | 256 / 3 / 8 |
| FFN / embeddings (species, item, ability, move) | x4 / 64, 32, 64, 64 | same |
| Attention | `flex_attention` (Triton, CUDA-only) | `scaled_dot_product_attention` + boolean mask (MPS ok) |
| Turn history | KV cache, 64 turns | none (current state only) |
| Policy head | linear on actor token | pointer heads: switch logits from team tokens, move logits from the active Pokémon's move vectors |
| Value head | 51-bin distributional | scalar |
| Environment | many Showdown servers + Ray workers | one server, poke-env, asyncio, batched inference |
| Training | BC → PPO | same: BC → self-play PPO |

The defaults in `model.py` build the ~4M model (`MiniPokeV1_4M` in ElsaMina).
`MiniPokeV1_1M` was trained with d_model 128, 4 heads, FFN x2 and embeddings
32, 16, 16, 32: set those in `ModelConfig` to train that size again. Checkpoints
store their own config, so loading or exporting one works whatever the defaults are.

## Setup (macOS, Apple Silicon)

Everything runs from this directory. The virtual environment, checkpoints,
exports and the Showdown clone stay here, ignored by git.

```bash
cd tools/minipoke

# 1. Python env
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt          # torch from PyPI includes MPS on macOS

# 2. One local Showdown server (Node 18+)
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
# Phase 1: imitate SimpleHeuristicsPlayer (a few minutes)
python train.py imitate --battles 3000 --epochs 8

# Phase 2: self-play PPO from the BC checkpoint
python train.py ppo --init checkpoints/bc.pt --iterations 300 --battles-per-iter 128

# Continue an interrupted PPO run: weights, optimizer and iteration
python train.py ppo --resume checkpoints/ppo_latest.pt --iterations 300 --battles-per-iter 128

# Evaluate any checkpoint
python train.py eval --ckpt checkpoints/ppo_latest.pt --opponent heuristic --battles 200
```

How it runs:
- **Training** (BC and PPO updates) uses `--device auto`, which picks MPS on an M3.
- **Rollout inference** uses `--infer-device cpu` by default. Weights are copied
  to the inference model after every update. At 4M parameters MPS is faster once
  batches reach ~32 states (about 6 ms vs 12 ms at 64), so try `--infer-device mps`
  and compare `st/s`; the CPU still wins for very small batches.
- `--concurrency` sets concurrent battles per player. Start at 32, then try
  64–128 while watching `st/s` in the logs.

Memory is not the limit on a 16 GB machine. The model plus Adam state is about
50 MB (so is each PPO checkpoint), and one PPO iteration (128 battles ≈ 7k states)
needs a few hundred MB.
Showdown and Python together stay under ~2 GB at moderate concurrency.

## Files

- `encoder.py` turns a poke-env `Battle` into tensors (12 Pokémon tokens, moves,
  field) plus the 14-action mask. Actions map onto poke-env's `SinglesEnv`
  action space, so orders are always built by poke-env itself.
- `model.py` holds `MiniPokeTransformer`.
- `players.py` has `InferenceBatcher` (async batching), `RLPlayer` (records
  trajectories with win ±1 and faint ±0.1 rewards, as in ps-ppo), and
  `TeacherPlayer` (labels states with the heuristic's moves).
- `ppo.py` has GAE, the PPO update and the behavior-cloning loop.
- `train.py` is the CLI.
- `export_onnx.py` exports a checkpoint to ONNX, with `encoder_spec.json`
  (vocabularies, item hashing, one-hot orderings, feature sizes).
- `make_fixtures.py` records battles for ElsaMina's parity tests.

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

### Releasing a new model N_SIZE

`N_SIZE` is the version and the size, `1_4M` for minipoke v1 with ~4M parameters.

1. Export the checkpoint into its model directory:
   ```bash
   python export_onnx.py --ckpt checkpoints/ppo_latest.pt --out ../../src/ElsaMina.Battles/Models/MiniPokeVN_SIZE
   ```
2. Record parity fixtures with the `encoder.py` this model was trained with
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
   to what `encoder.py` produced, every action mapped to the order poke-env sent, and
   the same logits in Python's onnxruntime and ElsaMina's.

Older models keep working as long as their encoder stays as it was. Their
fixtures were recorded with their own `encoder.py`: tag the commit of each
release (`git tag minipoke-v1-4m`) to regenerate them later.

Upgrading poke-env, or changing what `encoder.py` reads from it, means checking
the C# tracker against new fixtures too.

## What to expect

This is a proof of concept, not a reproduction of the 1900+ result. The
original's turn history matters for hidden information (inferring sets,
predicting switches), and so do its size and its 250M training states. A
realistic goal for this model on a laptop is to clearly beat
`SimpleHeuristicsPlayer` after BC plus a few hours of PPO.

Natural next steps, roughly in order of payoff:
1. **Opponent pool.** Keep past checkpoints and sample opponents from them
   instead of pure mirror self-play. This makes training more stable.
2. **Turn history.** Add a small per-turn "field/event" token and attend over
   the last N turns. This is the original's biggest feature missing here.
3. **Distributional value head** (two-hot, 51 bins), as in ps-ppo.

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
