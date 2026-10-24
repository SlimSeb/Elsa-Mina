using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// The ModelConfig fields that shape a minipoke-v2 model's observation (encoder_v2.py's flags).
/// </summary>
public sealed class EncoderSpecModel
{
    [JsonPropertyName("encoder_version")]
    public int EncoderVersion { get; set; }

    /// <summary>
    /// Candidate move slots per opponent Pokémon (K).
    /// </summary>
    [JsonPropertyName("cand_moves")]
    public int CandidateMoves { get; set; }

    /// <summary>
    /// The history features (hist_ids, hist_feats).
    /// </summary>
    [JsonPropertyName("history")]
    public bool History { get; set; }

    /// <summary>
    /// The last K battle events as tokens, 0 without them.
    /// </summary>
    [JsonPropertyName("history_tokens")]
    public int HistoryTokens { get; set; }

    [JsonPropertyName("matchup_tokens")]
    public bool MatchupTokens { get; set; }
}
