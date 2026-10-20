using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// Items have no vocabulary: their id is a hash bucket.
/// </summary>
public sealed class EncoderSpecItemHash
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "";

    [JsonPropertyName("buckets")]
    public int Buckets { get; set; }
}
