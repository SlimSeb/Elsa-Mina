using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// One input of the ONNX model, as a minipoke-v2 spec lists them.
/// </summary>
public sealed class EncoderSpecInput
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// "batch", then the sizes of one observation: ["batch", 6, 8].
    /// </summary>
    [JsonPropertyName("shape")]
    public List<JsonElement> Shape { get; set; } = [];

    /// <summary>
    /// "int64", "float32" or "bool".
    /// </summary>
    [JsonPropertyName("dtype")]
    public string DataType { get; set; } = "";

    /// <summary>
    /// The sizes of one observation, without the batch dimension.
    /// </summary>
    public int[] GetDimensions() => Shape.Skip(1).Select(dimension => dimension.GetInt32()).ToArray();
}
