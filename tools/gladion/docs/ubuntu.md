# Gladion on the Ubuntu machines

The README's pipeline on the two Ubuntu machines: the replay server (it collects the human replays and
builds the dataset) and the GPU box (Radeon RX 6600 XT, 8 GB, PyTorch for ROCm; it trains). What
changes from the Mac:

| Mac | Ubuntu |
|---|---|
| `caffeinate -is bash -c '...'` | `systemd-inhibit --what=idle:sleep --who=gladion --why=... bash -c '...'` |
| a terminal window | a `tmux` session: the queue stays a blocking foreground command, and survives SSH disconnects |
| Showdown clone in `tools/gladion/pokemon-showdown` | wherever it is: `$SHOWDOWN` below |
| MPS | `--device auto` picks the GPU (ROCm's PyTorch calls it `cuda`) |
| 16 GB, 3 data processes | `--processes` sized to the machine's RAM: ~0.8 GB per data process |

Every command runs from `tools/gladion`, in the venv, with `$SHOWDOWN` exported (step 0). `ubuntu`
and `gpu-box` stand for your SSH host names.

## 0. Once per machine

Skip what the machine already has.

```bash
sudo apt install -y git tmux python3-venv build-essential

# Node 22 or newer (Showdown refuses older ones; Ubuntu's apt nodejs is older): nvm
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.3/install.sh | bash
source ~/.bashrc
nvm install 22
node --version                                    # v22.x

git clone git@github.com:SlimSeb/Elsa-Mina.git ~/Elsa-Mina    # or: cd ~/Elsa-Mina && git pull
cd ~/Elsa-Mina/tools/gladion

# Python
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements-dev.txt
# GPU box only: PyTorch for ROCm instead of PyPI's CUDA build; X.Y = the ROCm version installed
# (pytorch.org's "Get started" page lists the indexes)
pip install --force-reinstall torch --index-url https://download.pytorch.org/whl/rocmX.Y
python -c "import torch; print(torch.cuda.is_available(), torch.cuda.get_device_name(0))"
#   False on the 6600 XT (gfx1032)? The usual fix: export HSA_OVERRIDE_GFX_VERSION=10.3.0

# The Showdown clone eval and the set priors use (skip if the box already has one)
git clone https://github.com/smogon/pokemon-showdown.git ~/pokemon-showdown
cd ~/pokemon-showdown && npm install && cp config/config-example.js config/config.js
node build && cd -

python -m pytest -q
```

At the start of every session:

```bash
tmux new -s gladion            # later: tmux attach -t gladion
cd ~/Elsa-Mina/tools/gladion && source .venv/bin/activate
export SHOWDOWN=$HOME/pokemon-showdown
```

`replays/`, `data/`, `checkpoints/`, `runs/` and `showdown-versions/` are not in git: copy what a step
needs between machines with `rsync -a`.

## 1. The dataset, on the replay server

The collector runs there (`collect_replays.py`, see the README). Re-simulation builds one Showdown
checkout per server version under `showdown-versions/` (git, npm and Node needed, ~40 s for the first,
a few seconds for each next one). The Mac's replays (Oct 30 to Nov 12) go into their own folder, not
into the server's `replays/human/gen9randombattle`: the Mac's `state.json` would replace the running
collector's cursors. Ids both machines have are counted once. The server skipped the Mac's span, and
`GladionV1_22M`'s dataset was built without the Mac's folder, so those 9.4k battles are missing from it.
Add the folder to the next build.

```bash
# From the Mac, once
ssh ubuntu 'mkdir -p Elsa-Mina/tools/gladion/replays/human-mac'
rsync -a --exclude state.json replays/human/gen9randombattle replays/human/gen9randombattle-first-step-zipped \
    ubuntu:Elsa-Mina/tools/gladion/replays/human-mac/
```

```bash
systemd-inhibit --what=idle:sleep --who=gladion --why="gladion human dataset" bash -c '
set -e
python collect_replays.py --summary
python resim.py check replays/human/gen9randombattle replays/human-mac --workers 4
python human_data.py replays/human/gen9randombattle replays/human-mac \
    --encoder-from ../../src/ElsaMina.Battles/Models/GladionV1_22M --out data/human-v21-$(date +%Y%m%d) --processes 4
'
```

- `resim.py check` prints the drop rate per server version (the Mac's 9.4k: 0% on all 6). A version
  that fails to build is listed in `showdown-versions/failed.json`, and its battles are dropped.
- `human_data.py` refuses to overwrite a dataset: give each build its own `--out`. Stopped, it resumes
  when run again with the same command line. Expect about 170 bytes a decision on disk.
- To train on the GPU box: `rsync -a data/human-v21-<date> gpu-box:Elsa-Mina/tools/gladion/data/`.

## 2. Training, on the GPU box

`D` is the dataset copied in step 1. Each `offline` run early-stops on the validation NLL and writes
`offline.pt` (best epoch), `offline_last.pt` (last epoch, for `--resume`) and `offline_metrics.json`.
The runs are named after the dataset (`R`): `checkpoints/v3/` and `runs/v3/` already hold Gladion v1's
runs, and a new run in a used folder starts over and overwrites its `offline.pt`. `--run` keeps the logs
apart too, since by default a run appends to `runs/<checkpoint folder name>/train.log`.

```bash
systemd-inhibit --what=idle:sleep --who=gladion --why="gladion offline" bash -c '
set -e
D=data/human-v21-<date>
R=$(basename $D)
SD="node pokemon-showdown start --no-security"
M=../../src/ElsaMina.Battles/Models
python train.py offline --data $D --size large --algo bc   --epochs 6 --seed 1 --ckpt-dir checkpoints/$R/bc-large --run $R-bc-large
python train.py offline --data $D --size large --algo awbc --epochs 6 --seed 1 --ckpt-dir checkpoints/$R/awbc-large --run $R-awbc-large
python train.py offline-eval --ckpt checkpoints/$R/bc-large/offline.pt checkpoints/$R/awbc-large/offline.pt \
    $M/GladionV1_22M --data $D --out runs/$R/offline-large.json
for c in bc-large awbc-large; do
  python train.py eval --ckpt checkpoints/$R/$c/offline.pt --vs-ckpt $M/GladionV1_22M --battles 4000 --processes 4 \
      --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
done
python train.py eval --ckpt checkpoints/$R/awbc-large/offline.pt --vs-ckpt $M/GladionV1_22M --search --vs-search-depth 1 \
    --battles 2000 --processes 4 --showdown-cmd "$SD" --showdown-cwd "$SHOWDOWN"
'
```

- `--epochs 6` is what trained `GladionV1_22M` on 258k battles: its best epoch was the 5th. The LR
  schedule spans the epochs asked for, so asking for more than early stopping will allow wastes the
  end of the schedule. BC only serves as the comparison: drop its line to halve the time.
- An epoch takes about 12 minutes per million training states (2 h 40 min for 258k battles), and the
  log prints nothing between epoch lines. To stop, press Ctrl-C about 30 s after an `epoch N/6` line.
- If a run stops, continue it with `python train.py offline --resume checkpoints/$R/<run>/offline_last.pt --data $D`.
  On a finished run it only recomputes the test metrics, so the same queue can be rerun with each
  `offline` line replaced by its `--resume` once `offline_last.pt` exists.
- The 22M memorizes one-hot labels: watch the validation NLL (`runs/<run>/train.log`); `--dropout 0.1`
  is an option for a new model. `--size wide-medium` (4.4M) trains much faster for quick comparisons.
- `--minibatch` 512 takes about 3.2 GiB with the desktop.

## Back to the Mac

What a release needs is the checkpoint and its logs:

```bash
# From the Mac, in tools/gladion; R = the run folder from step 2
mkdir -p checkpoints/$R/awbc-large runs
scp gpu-box:Elsa-Mina/tools/gladion/checkpoints/$R/awbc-large/offline.pt checkpoints/$R/awbc-large/
rsync -a gpu-box:Elsa-Mina/tools/gladion/runs/$R runs/
```

Then release it as the README says ([Replacing a released model](../README.md#replacing-a-released-model)
keeps the name `GladionV1_22M`, [Releasing a new model N_SIZE](../README.md#releasing-a-new-model-n_size)
adds one next to it).
