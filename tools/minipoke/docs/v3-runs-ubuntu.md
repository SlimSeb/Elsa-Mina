# minipoke v3: the long runs on Ubuntu

The queues of the README's [minipoke v3](../README.md#minipoke-v3-bigger-ppo-steps-then-human-replays)
section, for the Ubuntu machines: the replay server (the 50k human replays) and the GPU box (Radeon
RX 6600 XT, 8 GB, PyTorch for ROCm). What changes from the Mac:

| Mac | Ubuntu |
|---|---|
| `caffeinate -is bash -c '...'` | `systemd-inhibit --what=idle:sleep --who=minipoke --why=... bash -c '...'` |
| a terminal window | a `tmux` session: the queue stays a blocking foreground command, and survives SSH disconnects |
| Showdown clone in `~/Documents/minipoke/pokemon-showdown` | wherever it is: `$SHOWDOWN` below |
| MPS | `--device auto` picks the GPU (ROCm's PyTorch calls it `cuda`) |
| 16 GB, 3 data processes | `--processes` sized to the machine's RAM: ~0.8 GB per data process |

Every command runs from `tools/minipoke`, in the venv, with `$SHOWDOWN` exported (step 0). `ubuntu`
and `gpu-box` stand for your SSH host names.

## 0. Once per machine

Skip what the machine already has (the GPU box already trains minipoke: keep its venv and clone).

```bash
sudo apt install -y git tmux python3-venv build-essential

# Node 22 or newer (Showdown refuses older ones; Ubuntu's apt nodejs is older): nvm
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.3/install.sh | bash
source ~/.bashrc
nvm install 22
node --version                                    # v22.x

# The code: push feat/minipoke-v3 from the Mac first (it is only committed there)
git clone git@github.com:SlimSeb/Elsa-Mina.git ~/Elsa-Mina    # or: cd ~/Elsa-Mina && git fetch
cd ~/Elsa-Mina && git checkout feat/minipoke-v3
cd tools/minipoke

# Python
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements-dev.txt
# GPU box only: PyTorch for ROCm instead of PyPI's CUDA build; X.Y = the ROCm version installed
# (pytorch.org's "Get started" page lists the indexes)
pip install --force-reinstall torch --index-url https://download.pytorch.org/whl/rocmX.Y
python -c "import torch; print(torch.cuda.is_available(), torch.cuda.get_device_name(0))"
#   False on the 6600 XT (gfx1032)? The usual fix: export HSA_OVERRIDE_GFX_VERSION=10.3.0

# The Showdown clone training plays on (skip if the box already has one)
git clone https://github.com/smogon/pokemon-showdown.git ~/pokemon-showdown
cd ~/pokemon-showdown && npm install && cp config/config-example.js config/config.js
node build && cd -

# Tests (the re-simulation one builds nothing: it skips without Showdown 3da4dd39a2)
python -m pytest -q
```

At the start of every session:

```bash
tmux new -s minipoke            # later: tmux attach -t minipoke
cd ~/Elsa-Mina/tools/minipoke && source .venv/bin/activate
export SHOWDOWN=$HOME/pokemon-showdown
```

`checkpoints/`, `data/`, `replays/` and `showdown-versions/` are not in git. Copy what a step needs
from the Mac (the commands below say which), e.g. from the Mac:

```bash
cd ~/RiderProjects/Elsa-Mina/tools/minipoke
ssh ubuntu 'mkdir -p Elsa-Mina/tools/minipoke/checkpoints/b3 Elsa-Mina/tools/minipoke/checkpoints/v2-mr'
scp checkpoints/b3/distill.pt ubuntu:Elsa-Mina/tools/minipoke/checkpoints/b3/
scp checkpoints/v2-mr/best.pt ubuntu:Elsa-Mina/tools/minipoke/checkpoints/v2-mr/
```

## 1. The dataset, on the replay server

Re-simulation builds one Showdown checkout per server version under `showdown-versions/` (git, npm
and Node needed, ~40 s for the first, a few seconds for each next one). The Mac's replays (Oct 30 -
Nov 12) are not on the server: copy them over once, into their own folder. Not into the server's
`replays/human/gen9randombattle`: the Mac's `state.json` would replace the running collector's
cursors. The ids both machines have are counted once.

```bash
# From the Mac
ssh ubuntu 'mkdir -p Elsa-Mina/tools/minipoke/replays/human-mac'
rsync -a --exclude state.json replays/human/gen9randombattle replays/human/gen9randombattle-first-step-zipped \
    ubuntu:Elsa-Mina/tools/minipoke/replays/human-mac/
```

```bash
systemd-inhibit --what=idle:sleep --who=minipoke --why="minipoke human dataset" bash -c '
set -e
python collect_replays.py --summary
python resim.py check replays/human/gen9randombattle replays/human-mac --workers 4
python human_data.py replays/human/gen9randombattle replays/human-mac \
    --encoder-from ../../src/ElsaMina.Battles/Models/MiniPokeV2_1_22M --out data/human-v21 --processes 4
'
```

- `resim.py check` prints the drop rate per server version (the Mac's 9.4k: 0% on all 6). A version
  that fails to build is listed in `showdown-versions/failed.json`, and its battles are dropped.
- `human_data.py`: raise `--processes` if the server has the RAM (~0.8 GB each). On the M3, 9.4k battles
  took ~6 min with 3 processes, so expect ~30 min for 50k with 3, less with more. Stopped, it resumes
  when run again with the same command line. If `data/human-v21` already holds a dataset (it refuses
  to overwrite one), give another `--out` and use it in the commands below.
- The dataset is ~10 KB a decision on disk (the Mac's 554k decisions: 93 MB). To train on the GPU box:
  `rsync -a data/human-v21 gpu-box:Elsa-Mina/tools/minipoke/data/`.

## 2. Part A: the PPO step-size ablation (`small`)

Seven runs of ~1.5 h each on the M3 (`small` is bound by Showdown and poke-env, not by the GPU), then
their paired evals. Needs `checkpoints/b3/distill.pt` and `checkpoints/v2-mr/best.pt` from the Mac.

```bash
systemd-inhibit --what=idle:sleep --who=minipoke --why="minipoke v3 ablation" bash -c '
set -e
SD="node pokemon-showdown start --no-security"
run() {
  name=$1; shift
  python train.py ppo --init checkpoints/b3/distill.pt --recipe v2-mr --states 2500000 --seed 1 \
      --concurrency 32 --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN" \
      --ckpt-dir checkpoints/v3a/$name --run v3a-$name --export-dir export/v3a-$name "$@"
}
run ctl                              # v2-mr again: the noise floor
run lr2e-4      --lr 2e-4
run lr3e-4      --lr 3e-4
run floor0.3    --lr-min-ratio 0.3
run epochs4     --epochs 4
run epochs6     --epochs 6
run bpi384      --battles-per-iter 384
for name in ctl lr2e-4 lr3e-4 floor0.3 epochs4 epochs6 bpi384; do
  python train.py eval --ckpt checkpoints/v3a/$name/best.pt --vs-ckpt checkpoints/v2-mr/best.pt \
      --battles 4000 --processes 4 --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
done
python plot_metrics.py runs/v2-mr runs/v3a-* --out runs/v3a.png
'
```

`runs/v2-mr` only exists on the Mac: copy it too for the overlay, or drop it from the last line. If a
run stops (`set -e` ends the queue there), continue it with
`python train.py ppo --resume checkpoints/v3a/<name>/ppo_latest.pt --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"`,
then restart the queue from the next run. Then set `RECIPES["v3"]` in train.py to the changes that won.

## 3. Part C on the 4M size (`wide-medium`)

Needs `data/human-v21`. Offline training uses the GPU; its epochs are not timed yet (`small`: 80 s on
the M3).

```bash
systemd-inhibit --what=idle:sleep --who=minipoke --why="minipoke v3 4M" bash -c '
set -e
SD="node pokemon-showdown start --no-security"
M=../../src/ElsaMina.Battles/Models
python train.py offline --data data/human-v21 --size wide-medium --algo bc   --epochs 15 --seed 1 --ckpt-dir checkpoints/v3/bc-4m
python train.py offline --data data/human-v21 --size wide-medium --algo awbc --epochs 15 --seed 1 --ckpt-dir checkpoints/v3/awbc-4m
python train.py offline-eval --ckpt checkpoints/v3/bc-4m/offline.pt checkpoints/v3/awbc-4m/offline.pt \
    $M/MiniPokeV2_1_1M $M/MiniPokeV2_1_22M --data data/human-v21 --out runs/v3/offline-4m.json
for c in bc-4m awbc-4m; do
  python train.py eval --ckpt checkpoints/v3/$c/offline.pt --battles 4000 --processes 4 \
      --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
  python train.py eval --ckpt checkpoints/v3/$c/offline.pt --vs-ckpt $M/MiniPokeV2_1_22M --battles 4000 --processes 4 \
      --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
done
python train.py ppo --init checkpoints/v3/awbc-4m/offline.pt --recipe v3 \
    --ref-ckpt checkpoints/v3/awbc-4m/offline.pt --ref-kl-coef 0.05 --pool-extra checkpoints/v3/bc-4m/offline.pt \
    --states 5000000 --seed 1 --ckpt-dir checkpoints/v3/ppo-4m --run v3-ppo-4m --export-dir export/v3-ppo-4m \
    --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
python train.py eval --ckpt checkpoints/v3/ppo-4m/best.pt --vs-ckpt $M/MiniPokeV2_1_22M --battles 4000 --processes 4 \
    --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
python train.py eval --ckpt checkpoints/v3/ppo-4m/best.pt --vs-ckpt checkpoints/v3/awbc-4m/offline.pt --battles 4000 \
    --processes 4 --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
python train.py eval --ckpt checkpoints/v3/ppo-4m/best.pt --battles 4000 --processes 4 \
    --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
'
```

- Run it after the ablation has set `RECIPES["v3"]`, or the PPO step uses the provisional v3.
- If AWBC loses to BC in the offline-eval and the evals, start PPO from the BC model instead (both
  `--init` and `--ref-ckpt`).
- On the GPU box, add the eval against MiniPokeV2_1_4M (no released folder, its checkpoint is there):
  `python train.py eval --ckpt checkpoints/v3/ppo-4m/best.pt --vs-ckpt checkpoints/v2-1-4m/best.pt --battles 4000 --processes 4 --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"`.

## 4. `large` (22M), on the GPU box

The same stages with `--size large`. Two things the 8 GB card changes:
- The labels are one-hot over ~0.5-3M decisions: the 22M memorizes them. Early stopping on the
  validation NLL handles it (`--patience 2`); `--dropout 0.1` is an option for a new model.
- PPO adds the frozen reference model on the GPU to the 6.2 GiB a `large` run already peaks at. If it
  runs out of memory, `--minibatch 768` saves ~0.6 GiB (and `--resume` accepts it).

```bash
systemd-inhibit --what=idle:sleep --who=minipoke --why="minipoke v3 large" bash -c '
set -e
SD="node pokemon-showdown start --no-security"
M=../../src/ElsaMina.Battles/Models
python train.py offline --data data/human-v21 --size large --algo bc   --epochs 15 --seed 1 --ckpt-dir checkpoints/v3/bc-large
python train.py offline --data data/human-v21 --size large --algo awbc --epochs 15 --seed 1 --ckpt-dir checkpoints/v3/awbc-large
python train.py offline-eval --ckpt checkpoints/v3/bc-large/offline.pt checkpoints/v3/awbc-large/offline.pt \
    $M/MiniPokeV2_1_22M --data data/human-v21 --out runs/v3/offline-large.json
python train.py ppo --init checkpoints/v3/awbc-large/offline.pt --recipe v3 \
    --ref-ckpt checkpoints/v3/awbc-large/offline.pt --ref-kl-coef 0.05 --pool-extra checkpoints/v3/bc-large/offline.pt \
    --states 10000000 --seed 1 --ckpt-dir checkpoints/v3/ppo-large --run v3-ppo-large --export-dir export/v3-ppo-large \
    --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
python train.py eval --ckpt checkpoints/v3/ppo-large/best.pt --vs-ckpt $M/MiniPokeV2_1_22M --battles 4000 --processes 4 \
    --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
python train.py eval --ckpt checkpoints/v3/ppo-large/best.pt --vs-ckpt checkpoints/v2-1-4m/best.pt --battles 4000 \
    --processes 4 --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
python train.py eval --ckpt checkpoints/v3/ppo-large/best.pt --vs-ckpt $M/MiniPokeV2_1_22M --search --battles 2000 \
    --processes 4 --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
'
```

10M PPO states took ~11 h for the 22M (260 st/s); the reference model's extra forward pass adds a
little to `learn`.

## Back to the Mac

What the release needs is the checkpoint (or its export) and the logs:

```bash
# From the Mac, in tools/minipoke
mkdir -p checkpoints/v3/ppo-4m runs
scp ubuntu:Elsa-Mina/tools/minipoke/checkpoints/v3/ppo-4m/best.pt checkpoints/v3/ppo-4m/
rsync -a ubuntu:Elsa-Mina/tools/minipoke/runs/v3-ppo-4m runs/
rsync -a ubuntu:Elsa-Mina/tools/minipoke/runs/v3 runs/
```

Then release it as the README's [Releasing a new model N_SIZE](../README.md#releasing-a-new-model-n_size)
says, once its name is decided (its observation is V2.1's).
