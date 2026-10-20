using System.Text.Json.Serialization;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPoke;

/// <summary>
/// What encoder.py produced at one choose_move, after the first <see cref="LineCount"/> lines. Arrays are flat,
/// row-major.
/// </summary>
public class MiniPokeFixtureDecision
{
    [JsonPropertyName("line_count")]
    public int LineCount { get; set; }

    [JsonPropertyName("mon_ids")]
    public long[] MonIds { get; set; } = [];

    [JsonPropertyName("move_ids")]
    public long[] MoveIds { get; set; } = [];

    [JsonPropertyName("mon_feats")]
    public double[] MonFeatures { get; set; } = [];

    [JsonPropertyName("move_feats")]
    public double[] MoveFeatures { get; set; } = [];

    [JsonPropertyName("field")]
    public double[] Field { get; set; } = [];

    [JsonPropertyName("mask")]
    public bool[] Mask { get; set; } = [];

    /// <summary>
    /// poke-env's battle.team keys, in order.
    /// </summary>
    [JsonPropertyName("team_idents")]
    public List<string> TeamIdents { get; set; } = [];

    /// <summary>
    /// Legal action → the order poke-env sends for it ("/choose move earthquake terastallize",
    /// "/choose switch Garchomp"), or null when it couldn't build one.
    /// </summary>
    [JsonPropertyName("orders")]
    public Dictionary<string, string> Orders { get; set; } = [];

    /// <summary>
    /// minipoke.onnx's logits for this observation, run by onnxruntime in Python.
    /// </summary>
    [JsonPropertyName("logits")]
    public float[] Logits { get; set; }

    [JsonPropertyName("value")]
    public float Value { get; set; }
}
