"""
Render a run's metrics.csv to a PNG.

    python plot_metrics.py runs/medium-20261021-1400            # -> runs/.../metrics.png
    python plot_metrics.py runs/a runs/b --out compare.png       # several runs on the same axes
    python plot_metrics.py runs/a runs/b --x iteration           # x: total_states (default),
                                                                 # iteration or hours
"""

from __future__ import annotations

import argparse
import csv
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib.ticker import EngFormatter  # noqa: E402

PANELS = [
    ("Win rate (greedy eval)", ["eval_wr_heuristic", "confirm_wr", "eval_wr_pool"], (0, 1)),
    ("Win rate (training battles)", ["train_wr_heuristic", "train_wr_pool"], (0, 1)),
    ("Throughput (states/s)", ["states_per_s"], None),
    ("Policy loss", ["pi_loss"], None),
    ("Value loss", ["v_loss", "v_mse"], None),
    ("Explained variance", ["explained_var"], None),
    ("Entropy", ["entropy"], None),
    ("Approx. KL / clip fraction", ["kl", "clipfrac"], None),
    ("Learning rate", ["lr"], None),
    ("Elo (learner; random = 0)", ["elo"], None),
    ("Pool / rewards", ["pool_hardest_wr", "faint_reward"], None),
    ("PPO epochs run", ["epochs"], None),
]


def read(path: Path):
    with open(path, newline="") as f:
        rows = list(csv.DictReader(f))
    cols = {}
    for k in rows[0] if rows else []:
        cols[k] = [float(r[k]) if r[k] not in ("", None) and k != "time" else None for r in rows]
    return cols


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+", help="run directories (or metrics.csv files)")
    ap.add_argument("--x", default="total_states", choices=["total_states", "iteration", "hours"])
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    paths = [Path(r) if r.endswith(".csv") else Path(r) / "metrics.csv" for r in args.runs]
    rows = (len(PANELS) + 2) // 3
    fig, axes = plt.subplots(rows, 3, figsize=(15, 3.7 * rows), constrained_layout=True)
    for path in paths:
        cols = read(path)
        name = path.parent.name
        x = cols.get(args.x, [])
        for ax, (title, keys, ylim) in zip(axes.flat, PANELS):
            for k in keys:
                pts = [(a, b) for a, b in zip(x, cols.get(k, [])) if a is not None and b is not None]
                if pts:
                    label = k if len(paths) == 1 else f"{name}: {k}"
                    ax.plot(*zip(*pts), marker="o" if len(pts) < 30 else None, ms=3, label=label)
            ax.set_title(title)
            ax.set_xlabel(args.x)
            if args.x == "total_states":
                ax.xaxis.set_major_formatter(EngFormatter())  # 2M, not 2000000
            if ylim:
                ax.set_ylim(*ylim)
            ax.grid(alpha=0.3)
    for ax in axes.flat:
        if ax.lines:
            ax.legend(fontsize=8)
    out = Path(args.out) if args.out else paths[0].with_suffix(".png")
    fig.savefig(out, dpi=110)
    print(f"wrote {out}")


if __name__ == "__main__":
    main()
