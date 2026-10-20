using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// Embedding ids by name, as encoder.py built them from poke-env's gen 9 data.
/// </summary>
public sealed class EncoderSpecVocabularies
{
    [JsonPropertyName("species")]
    public Dictionary<string, long> Species { get; set; } = [];

    [JsonPropertyName("move")]
    public Dictionary<string, long> Move { get; set; } = [];

    [JsonPropertyName("ability")]
    public Dictionary<string, long> Ability { get; set; } = [];
}
