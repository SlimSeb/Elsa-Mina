"""
MiniPokeTransformer — a small take on ps-ppo's PokeTransformer that runs on
Apple Silicon (MPS), CUDA or CPU. Presets: "small" (~0.9M parameters, the
MiniPokeV1_1M architecture), "medium" (~3.8M) and "wide" (~4.1M, the
MiniPokeV1_4M architecture).

Differences from the original:
  * No flex_attention / Triton: plain F.scaled_dot_product_attention with a
    boolean mask (supported on MPS).
  * d_model 128 (small) or 256 (medium, wide) instead of 512. FFN x2, except
    wide, whose layers (3), heads (8), FFN x4 and embedding sizes match the original.
  * No turn history / KV cache and no JEPA auxiliary loss (each decision
    only sees the current state).
  * Pointer-style policy head: switch logits come from our 6 Pokémon tokens,
    move logits from the active Pokémon's move vectors. This gives a small
    model a much easier job than a plain 14-way linear head.

Tokens: [actor, critic, field, 6 x ours, 6 x theirs] = 15
Attention mask ("poke-mask", as in the original):
  * state tokens (field + 12 mons) only attend to each other
  * actor / critic attend to the state and to themselves, not to each other

encoder_version 2 (minipoke-v2) reads encoder_v2.py's set priors: each opponent
Pokémon's candidate moves go through the same move MLP as known moves (with a
known/candidate flag and their fraction), and their fraction-weighted mean, the
ability / item prior embeddings and the prior features join its token's input.

move_tokens: one more token per (Pokémon, move slot), its moves and, with
encoder v2, the opponents' candidate moves, after the 15 above:
    [actor, critic, field, 12 mons, 12 x 4 known moves, 6 x K candidate moves]
plus learned embeddings of the owning Pokémon slot and of known / candidate.
They are state tokens (same mask rules); empty slots are masked out as keys.
The move head then reads our active Pokémon's move tokens after the transformer
instead of the move vectors before it. The Pokémon tokens no longer concatenate
their move vectors.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass, fields, replace
from typing import Dict, Optional, Tuple

import torch
import torch.nn as nn
import torch.nn.functional as F

from encoder import ACT_DIM, F_FIELD, F_MON, F_MOVE, N_MONS, N_MOVES, N_SLOTS, VOCAB_SIZES
from encoder_v2 import F_PRIOR

N_TOKENS = 3 + N_MONS
ACTOR, CRITIC, FIELD_TOK = 0, 1, 2
MON0 = 3
MOVE0 = N_TOKENS        # first move token (move_tokens)


@dataclass
class ModelConfig:
    """Stored in every checkpoint. New fields need a default that rebuilds the
    old architecture, so older checkpoints keep loading."""
    d_model: int = 128
    n_layers: int = 3
    n_heads: int = 4
    ff_mult: float = 2.0
    dropout: float = 0.0
    species_dim: int = 32
    item_dim: int = 16
    ability_dim: int = 16
    move_dim: int = 32
    # Value head: 1 = scalar (V1). > 1 = distributional, trained with two-hot
    # cross-entropy over `value_bins` evenly spaced returns in [value_min, value_max].
    # forward() always returns the scalar expectation either way.
    value_bins: int = 1
    value_min: float = -1.6
    value_max: float = 1.6
    # Observation layout: 1 = encoder.py (V1), 2 = encoder_v2.py (V1 + random battle
    # set priors), whose opponent Pokémon have up to `cand_moves` candidate moves.
    encoder_version: int = 1
    cand_moves: int = 8
    # One token per (Pokémon, move slot); the move head reads them after the transformer
    move_tokens: bool = False

    @classmethod
    def from_dict(cls, d: dict) -> "ModelConfig":
        known = {f.name for f in fields(cls)}
        return cls(**{k: v for k, v in d.items() if k in known})

    def to_dict(self) -> dict:
        return asdict(self)


PRESETS: Dict[str, ModelConfig] = {
    "small": ModelConfig(),
    "medium": ModelConfig(d_model=256, n_layers=4, n_heads=8, ff_mult=2.0,
                          species_dim=64, item_dim=32, ability_dim=32, move_dim=64,
                          value_bins=51),
    "wide": ModelConfig(d_model=256, n_layers=3, n_heads=8, ff_mult=4.0,
                        species_dim=64, item_dim=32, ability_dim=64, move_dim=64),
}


def size_of(cfg: ModelConfig) -> str:
    """The preset whose transformer shape matches cfg ("small" when none does)."""
    for name, p in PRESETS.items():
        if (cfg.d_model, cfg.n_layers, cfg.n_heads) == (p.d_model, p.n_layers, p.n_heads):
            return name
    return "small"


def preset(name: str, **overrides) -> ModelConfig:
    return replace(PRESETS[name], **{k: v for k, v in overrides.items() if v is not None})


def mlp(in_d: int, out_d: int, hidden_mult: int = 2) -> nn.Sequential:
    return nn.Sequential(
        nn.Linear(in_d, out_d * hidden_mult),
        nn.GELU(),
        nn.Linear(out_d * hidden_mult, out_d),
        nn.LayerNorm(out_d),
    )


def build_attention_mask(n_tokens: int = N_TOKENS) -> torch.Tensor:
    """Boolean [T, T] mask, True = may attend. Every token from FIELD_TOK on is a
    state token."""
    m = torch.zeros(n_tokens, n_tokens, dtype=torch.bool)
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
    def __init__(self, cfg: Optional[ModelConfig] = None):
        super().__init__()
        cfg = cfg or ModelConfig()
        self.cfg = cfg
        d = cfg.d_model

        self.species_emb = nn.Embedding(VOCAB_SIZES["species"], cfg.species_dim, padding_idx=0)
        self.item_emb = nn.Embedding(VOCAB_SIZES["item"], cfg.item_dim, padding_idx=0)
        self.ability_emb = nn.Embedding(VOCAB_SIZES["ability"], cfg.ability_dim, padding_idx=0)
        self.move_emb = nn.Embedding(VOCAB_SIZES["move"], cfg.move_dim, padding_idx=0)

        if cfg.encoder_version not in (1, 2):
            raise ValueError(f"unknown encoder_version {cfg.encoder_version}")
        self.v2 = cfg.encoder_version == 2
        self.n_move_tokens = 0
        if cfg.move_tokens:
            self.n_move_tokens = N_MONS * N_MOVES + (N_SLOTS * cfg.cand_moves if self.v2 else 0)
        self.n_tokens = N_TOKENS + self.n_move_tokens
        # v2: + known/candidate flag and fraction of consistent roles (1 for known moves)
        self.move_net = mlp(cfg.move_dim + F_MOVE + (2 if self.v2 else 0), d)
        moves_in = 0 if cfg.move_tokens else N_MOVES * d   # move tokens: moves are tokens
        mon_in = cfg.species_dim + cfg.item_dim + cfg.ability_dim + F_MON + moves_in
        if self.v2:  # candidate moves' weighted mean, ability / item priors, prior features
            mon_in += moves_in // N_MOVES + cfg.ability_dim + cfg.item_dim + F_PRIOR
        self.mon_net = mlp(mon_in, d)
        # Shared, not a parameter: what encoder_v2 needs to rebuild this model's
        # observations (saved in checkpoints and exported as set_priors.json).
        self.priors = None
        self.field_net = mlp(F_FIELD, d)

        self.side_emb = nn.Parameter(torch.zeros(2, d))       # ours / theirs
        self.actor_tok = nn.Parameter(torch.randn(1, 1, d) * 0.02)
        self.critic_tok = nn.Parameter(torch.randn(1, 1, d) * 0.02)

        self.layers = nn.ModuleList(
            EncoderLayer(d, cfg.n_heads, cfg.ff_mult, cfg.dropout) for _ in range(cfg.n_layers)
        )
        self.final_norm = nn.LayerNorm(d)
        self.register_buffer("attn_mask", build_attention_mask(self.n_tokens), persistent=False)

        # Policy: pointer heads
        self.switch_head = nn.Sequential(nn.Linear(2 * d, d), nn.GELU(), nn.Linear(d, 1))
        self.move_head = nn.Sequential(nn.Linear(3 * d, d), nn.GELU(), nn.Linear(d, 2))
        # Value: one scalar, or logits over a fixed support of returns
        self.value_head = nn.Sequential(nn.Linear(d, d), nn.GELU(), nn.Linear(d, cfg.value_bins))
        if cfg.value_bins > 1:
            self.register_buffer("value_support",
                                 torch.linspace(cfg.value_min, cfg.value_max, cfg.value_bins),
                                 persistent=False)
        if cfg.move_tokens:
            self.move_owner_emb = nn.Parameter(torch.randn(N_MONS, d) * 0.02)  # owning slot
            self.move_known_emb = nn.Parameter(torch.randn(2, d) * 0.02)       # candidate / known
            self.register_buffer("self_attend", torch.eye(self.n_tokens, dtype=torch.bool),
                                 persistent=False)
            self.register_buffer("move_offsets", torch.arange(N_MOVES), persistent=False)

        self.apply(self._init)

    @staticmethod
    def _init(m: nn.Module) -> None:
        if isinstance(m, nn.Linear):
            nn.init.normal_(m.weight, std=0.02)
            nn.init.zeros_(m.bias)
        elif isinstance(m, nn.Embedding):
            nn.init.normal_(m.weight, std=0.02)

    @property
    def distributional(self) -> bool:
        return self.cfg.value_bins > 1

    def _candidate_vectors(self, obs: Dict[str, torch.Tensor]) -> torch.Tensor:
        """Opponent candidate moves through the move MLP: [B, 6, K, d], 0 when empty."""
        cand_feats = obs["cand_feats"]
        present = cand_feats[..., 0:1]
        x = torch.cat([self.move_emb(obs["cand_ids"]), cand_feats[..., :F_MOVE],
                       torch.zeros_like(present), cand_feats[..., F_MOVE:]], dim=-1)
        return self.move_net(x) * present

    def _prior_inputs(self, obs: Dict[str, torch.Tensor],
                      cand_vec: Optional[torch.Tensor]) -> torch.Tensor:
        """Extra token input of the 6 opponent Pokémon: [B, 6, (d) + ability + item +
        F_PRIOR], with the candidates' weighted mean when they are not tokens."""
        parts = []
        if cand_vec is not None:
            frac = obs["cand_feats"][..., F_MOVE:] * obs["cand_feats"][..., 0:1]  # [B, 6, K, 1]
            parts.append((cand_vec * frac).sum(2) / frac.sum(2).clamp_min(1e-6))
        ids, w = obs["prior_ids"], obs["prior_w"].unsqueeze(-1)              # [B, 6, 2, A]
        parts.append((self.ability_emb(ids[:, :, 0]) * w[:, :, 0]).sum(2))
        parts.append((self.item_emb(ids[:, :, 1]) * w[:, :, 1]).sum(2))
        return torch.cat(parts + [obs["prior_feats"]], dim=-1)

    def _move_tokens(self, obs: Dict[str, torch.Tensor], move_vec: torch.Tensor,
                     cand_vec: Optional[torch.Tensor]) -> Tuple[torch.Tensor, torch.Tensor]:
        """Move tokens [B, n_move_tokens, d] and whether each slot holds a move [B, n]."""
        B, d = move_vec.shape[0], self.cfg.d_model
        known = move_vec + self.move_owner_emb[:, None, :] + self.move_known_emb[1]
        tokens = [known.reshape(B, N_MONS * N_MOVES, d)]
        present = [obs["move_feats"][..., 0].reshape(B, N_MONS * N_MOVES) > 0]
        if cand_vec is not None:
            cand = cand_vec + self.move_owner_emb[N_SLOTS:, None, :] + self.move_known_emb[0]
            tokens.append(cand.reshape(B, -1, d))
            present.append(obs["cand_feats"][..., 0].reshape(B, -1) > 0)
        return torch.cat(tokens, dim=1), torch.cat(present, dim=1)

    def attention_mask(self, key_present: Optional[torch.Tensor]) -> torch.Tensor:
        """[T, T] (every token present), or [B, 1, T, T] where tokens that are not
        present are no one's key but their own (so no row is empty)."""
        if key_present is None:
            return self.attn_mask
        return (self.attn_mask & (key_present[:, None, :] | self.self_attend)).unsqueeze(1)

    def embed(self, obs: Dict[str, torch.Tensor]):
        """-> (tokens [B, T, d], key_present [B, T] or None, move_vec [B, 12, 4, d])."""
        mon_ids, move_ids = obs["mon_ids"], obs["move_ids"]
        mon_feats, move_feats = obs["mon_feats"], obs["move_feats"]
        B = mon_ids.shape[0]
        d = self.cfg.d_model

        # --- Move vectors [B, 12, 4, d]
        move_present = move_feats[..., 0:1]          # zero out empty move slots
        mv = [self.move_emb(move_ids), move_feats]
        if self.v2:
            mv += [move_present, move_present]       # known, fraction 1
        move_vec = self.move_net(torch.cat(mv, dim=-1))
        move_vec = move_vec * move_present
        cand_vec = self._candidate_vectors(obs) if self.v2 else None

        # --- Pokémon tokens [B, 12, d]
        parts = [
            self.species_emb(mon_ids[..., 0]),
            self.item_emb(mon_ids[..., 1]),
            self.ability_emb(mon_ids[..., 2]),
            mon_feats,
        ]
        if not self.cfg.move_tokens:
            parts.append(move_vec.reshape(B, N_MONS, N_MOVES * d))
        if self.v2:
            theirs = self._prior_inputs(obs, None if self.cfg.move_tokens else cand_vec)
            parts.append(torch.cat([torch.zeros_like(theirs), theirs], dim=1))
        mons = self.mon_net(torch.cat(parts, dim=-1))
        side = torch.cat(
            [self.side_emb[0].expand(N_SLOTS, d), self.side_emb[1].expand(N_SLOTS, d)], dim=0
        )
        mons = mons + side

        field = self.field_net(obs["field"]).unsqueeze(1)
        tokens = [self.actor_tok.expand(B, 1, d), self.critic_tok.expand(B, 1, d), field, mons]
        key_present = None
        if self.cfg.move_tokens:
            moves, present = self._move_tokens(obs, move_vec, cand_vec)
            tokens.append(moves)
            key_present = torch.cat([present.new_ones(B, N_TOKENS), present], dim=1)
        return torch.cat(tokens, dim=1), key_present, move_vec

    def forward(self, obs: Dict[str, torch.Tensor]) -> Tuple[torch.Tensor, torch.Tensor]:
        """Returns masked policy logits [B, 14] and value [B] (a scalar expected return)."""
        logits, value, _ = self.forward_train(obs)
        return logits, value

    def forward_train(self, obs: Dict[str, torch.Tensor]
                      ) -> Tuple[torch.Tensor, torch.Tensor, Optional[torch.Tensor]]:
        """Like forward(), plus the value logits [B, bins] (None for a scalar head)."""
        mon_feats = obs["mon_feats"]
        x, key_present, move_vec = self.embed(obs)
        B, d = x.shape[0], self.cfg.d_model
        mask = self.attention_mask(key_present)
        for layer in self.layers:
            x = layer(x, mask)
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
        if self.cfg.move_tokens:  # its move tokens, after the transformer
            idx = (MOVE0 + active_idx * N_MOVES).unsqueeze(1) + self.move_offsets   # [B, 4]
            act_moves = torch.gather(x, 1, idx.unsqueeze(-1).expand(-1, -1, d))
        else:
            act_moves = move_vec[ar, active_idx]              # [B, 4, d]
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

        if not self.distributional:
            return logits, self.value_head(critic).squeeze(-1), None
        value_logits = self.value_head(critic)
        value = torch.softmax(value_logits, dim=-1) @ self.value_support
        return logits, value, value_logits


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


def describe(model: nn.Module) -> str:
    """Parameter count, total and per top-level module."""
    groups: Dict[str, int] = {}
    for name, p in model.named_parameters():
        key = name.split(".")[0]
        groups[key] = groups.get(key, 0) + p.numel()
    parts = ", ".join(f"{k} {v / 1e3:.0f}k" for k, v in groups.items() if v >= 1000)
    return f"{count_params(model):,} params ({parts})"


def build_model(state: dict) -> MiniPokeTransformer:
    """Rebuilds a model from a checkpoint dict ({"model": state_dict, "cfg": {...}},
    plus "set_priors" for an encoder v2 model)."""
    model = MiniPokeTransformer(ModelConfig.from_dict(state["cfg"]))
    model.load_state_dict(state["model"])
    if state.get("set_priors") is not None:
        from setpriors import SetPriors
        model.priors = SetPriors.from_dict(state["set_priors"])
    return model


assert ACT_DIM == N_SLOTS + 2 * N_MOVES
