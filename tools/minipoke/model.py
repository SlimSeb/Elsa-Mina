"""
MiniPokeTransformer — a ~1M-parameter take on ps-ppo's PokeTransformer that
runs on Apple Silicon (MPS), CUDA or CPU.

Differences from the original:
  * No flex_attention / Triton: plain F.scaled_dot_product_attention with a
    boolean mask (supported on MPS).
  * d_model 128 instead of 512, 3 layers, FFN x2.
  * No turn history / KV cache and no JEPA auxiliary loss (each decision
    only sees the current state).
  * Pointer-style policy head: switch logits come from our 6 Pokémon tokens,
    move logits from the active Pokémon's move vectors. This gives a small
    model a much easier job than a plain 14-way linear head.

Tokens: [actor, critic, field, 6 x ours, 6 x theirs] = 15
Attention mask ("poke-mask", as in the original):
  * state tokens (field + 12 mons) only attend to each other
  * actor / critic attend to the state and to themselves, not to each other
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Dict, Tuple

import torch
import torch.nn as nn
import torch.nn.functional as F

from encoder import ACT_DIM, F_FIELD, F_MON, F_MOVE, N_MONS, N_MOVES, N_SLOTS, VOCAB_SIZES

N_TOKENS = 3 + N_MONS
ACTOR, CRITIC, FIELD_TOK = 0, 1, 2
MON0 = 3


@dataclass
class ModelConfig:
    d_model: int = 128
    n_layers: int = 3
    n_heads: int = 4
    ff_mult: float = 2.0
    dropout: float = 0.0
    species_dim: int = 32
    item_dim: int = 16
    ability_dim: int = 16
    move_dim: int = 32


def mlp(in_d: int, out_d: int, hidden_mult: int = 2) -> nn.Sequential:
    return nn.Sequential(
        nn.Linear(in_d, out_d * hidden_mult),
        nn.GELU(),
        nn.Linear(out_d * hidden_mult, out_d),
        nn.LayerNorm(out_d),
    )


def build_attention_mask() -> torch.Tensor:
    """Boolean [T, T] mask, True = may attend."""
    m = torch.zeros(N_TOKENS, N_TOKENS, dtype=torch.bool)
    m[FIELD_TOK:, FIELD_TOK:] = True       # state <-> state
    m[ACTOR, FIELD_TOK:] = True            # actor -> state
    m[CRITIC, FIELD_TOK:] = True           # critic -> state
    m[ACTOR, ACTOR] = True
    m[CRITIC, CRITIC] = True
    return m


class EncoderLayer(nn.Module):
    """Pre-LN transformer block using SDPA (works on MPS)."""

    def __init__(self, d: int, n_heads: int, ff_mult: float, dropout: float):
        super().__init__()
        self.n_heads = n_heads
        self.norm1 = nn.LayerNorm(d)
        self.qkv = nn.Linear(d, 3 * d)
        self.proj = nn.Linear(d, d)
        self.norm2 = nn.LayerNorm(d)
        self.ff = nn.Sequential(
            nn.Linear(d, int(d * ff_mult)),
            nn.GELU(),
            nn.Linear(int(d * ff_mult), d),
        )
        self.drop = nn.Dropout(dropout)

    def forward(self, x: torch.Tensor, mask: torch.Tensor) -> torch.Tensor:
        B, T, D = x.shape
        h = self.norm1(x)
        q, k, v = (
            self.qkv(h).view(B, T, 3, self.n_heads, D // self.n_heads).permute(2, 0, 3, 1, 4)
        )
        a = F.scaled_dot_product_attention(q, k, v, attn_mask=mask)
        x = x + self.drop(self.proj(a.transpose(1, 2).reshape(B, T, D)))
        return x + self.drop(self.ff(self.norm2(x)))


class MiniPokeTransformer(nn.Module):
    def __init__(self, cfg: ModelConfig = ModelConfig()):
        super().__init__()
        self.cfg = cfg
        d = cfg.d_model

        self.species_emb = nn.Embedding(VOCAB_SIZES["species"], cfg.species_dim, padding_idx=0)
        self.item_emb = nn.Embedding(VOCAB_SIZES["item"], cfg.item_dim, padding_idx=0)
        self.ability_emb = nn.Embedding(VOCAB_SIZES["ability"], cfg.ability_dim, padding_idx=0)
        self.move_emb = nn.Embedding(VOCAB_SIZES["move"], cfg.move_dim, padding_idx=0)

        self.move_net = mlp(cfg.move_dim + F_MOVE, d)
        mon_in = cfg.species_dim + cfg.item_dim + cfg.ability_dim + F_MON + N_MOVES * d
        self.mon_net = mlp(mon_in, d)
        self.field_net = mlp(F_FIELD, d)

        self.side_emb = nn.Parameter(torch.zeros(2, d))       # ours / theirs
        self.actor_tok = nn.Parameter(torch.randn(1, 1, d) * 0.02)
        self.critic_tok = nn.Parameter(torch.randn(1, 1, d) * 0.02)

        self.layers = nn.ModuleList(
            EncoderLayer(d, cfg.n_heads, cfg.ff_mult, cfg.dropout) for _ in range(cfg.n_layers)
        )
        self.final_norm = nn.LayerNorm(d)
        self.register_buffer("attn_mask", build_attention_mask(), persistent=False)

        # Policy: pointer heads
        self.switch_head = nn.Sequential(nn.Linear(2 * d, d), nn.GELU(), nn.Linear(d, 1))
        self.move_head = nn.Sequential(nn.Linear(3 * d, d), nn.GELU(), nn.Linear(d, 2))
        # Value
        self.value_head = nn.Sequential(nn.Linear(d, d), nn.GELU(), nn.Linear(d, 1))

        self.apply(self._init)

    @staticmethod
    def _init(m: nn.Module) -> None:
        if isinstance(m, nn.Linear):
            nn.init.normal_(m.weight, std=0.02)
            nn.init.zeros_(m.bias)
        elif isinstance(m, nn.Embedding):
            nn.init.normal_(m.weight, std=0.02)

    def forward(self, obs: Dict[str, torch.Tensor]) -> Tuple[torch.Tensor, torch.Tensor]:
        """Returns masked policy logits [B, 14] and value [B]."""
        mon_ids, move_ids = obs["mon_ids"], obs["move_ids"]
        mon_feats, move_feats = obs["mon_feats"], obs["move_feats"]
        B = mon_ids.shape[0]
        d = self.cfg.d_model

        # --- Move vectors [B, 12, 4, d]
        mv = torch.cat([self.move_emb(move_ids), move_feats], dim=-1)
        move_vec = self.move_net(mv)
        move_present = move_feats[..., 0:1]          # zero out empty move slots
        move_vec = move_vec * move_present

        # --- Pokémon tokens [B, 12, d]
        mon_in = torch.cat(
            [
                self.species_emb(mon_ids[..., 0]),
                self.item_emb(mon_ids[..., 1]),
                self.ability_emb(mon_ids[..., 2]),
                mon_feats,
                move_vec.reshape(B, N_MONS, N_MOVES * d),
            ],
            dim=-1,
        )
        mons = self.mon_net(mon_in)
        side = torch.cat(
            [self.side_emb[0].expand(N_SLOTS, d), self.side_emb[1].expand(N_SLOTS, d)], dim=0
        )
        mons = mons + side

        field = self.field_net(obs["field"]).unsqueeze(1)
        x = torch.cat(
            [self.actor_tok.expand(B, 1, d), self.critic_tok.expand(B, 1, d), field, mons], dim=1
        )
        for layer in self.layers:
            x = layer(x, self.attn_mask)
        x = self.final_norm(x)

        actor = x[:, ACTOR]                              # [B, d]
        critic = x[:, CRITIC]
        our_mons = x[:, MON0 : MON0 + N_SLOTS]           # [B, 6, d]

        # Switch logits: one per team slot
        sw_in = torch.cat([actor.unsqueeze(1).expand(-1, N_SLOTS, -1), our_mons], dim=-1)
        switch_logits = self.switch_head(sw_in).squeeze(-1)   # [B, 6]

        # Move logits: the active Pokémon's 4 moves, normal + tera
        active_flag = mon_feats[:, :N_SLOTS, 3]               # [B, 6]
        active_idx = active_flag.argmax(dim=-1)               # [B]
        ar = torch.arange(B, device=x.device)
        act_moves = move_vec[ar, active_idx]                  # [B, 4, d]
        act_tok = our_mons[ar, active_idx]                    # [B, d]
        mv_in = torch.cat(
            [
                actor.unsqueeze(1).expand(-1, N_MOVES, -1),
                act_tok.unsqueeze(1).expand(-1, N_MOVES, -1),
                act_moves,
            ],
            dim=-1,
        )
        mv_logits = self.move_head(mv_in)                     # [B, 4, 2]
        logits = torch.cat([switch_logits, mv_logits[..., 0], mv_logits[..., 1]], dim=-1)

        mask = obs["mask"]
        # Guard: if a row has no legal action (shouldn't happen), unmask all
        # (float reduction rather than bool .any() so the ONNX export stays simple)
        has_legal = mask.float().amax(dim=-1, keepdim=True) > 0
        mask = mask | ~has_legal
        logits = logits.masked_fill(~mask, -1e9)

        value = self.value_head(critic).squeeze(-1)
        return logits, value


def to_tensors(batch, device) -> Dict[str, torch.Tensor]:
    """numpy batch (from encoder.collate) -> tensors on device."""
    out = {}
    for k, v in batch.items():
        t = torch.from_numpy(v)
        if t.dtype == torch.float64:
            t = t.float()
        out[k] = t.to(device, non_blocking=True)
    return out


def pick_device(pref: str = "auto") -> torch.device:
    if pref != "auto":
        return torch.device(pref)
    if torch.backends.mps.is_available():
        return torch.device("mps")
    if torch.cuda.is_available():
        return torch.device("cuda")
    return torch.device("cpu")


def count_params(model: nn.Module) -> int:
    return sum(p.numel() for p in model.parameters())


assert ACT_DIM == N_SLOTS + 2 * N_MOVES
