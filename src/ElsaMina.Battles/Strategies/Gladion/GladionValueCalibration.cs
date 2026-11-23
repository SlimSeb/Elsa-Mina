using System.Text.Json;
using System.Text.Json.Serialization;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies.Gladion;

/// <summary>
/// value_calibration.json, next to a model (tools/gladion/analyze_replays.py --write-calibration): an affine map
/// from the model's value to the return it actually got on the ladder. The value head learned self-play, where
/// both sides are as strong; against humans it is optimistic.
/// </summary>
public sealed class GladionValueCalibration
{
    public static readonly GladionValueCalibration Identity = new();

    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1.0;

    [JsonPropertyName("offset")]
    public double Offset { get; set; }

    /// <summary>
    /// The expected return for a model value.
    /// </summary>
    public double Apply(double value) => Scale * value + Offset;

    /// <summary>
    /// The calibration at <paramref name="path"/>, or the identity when there is none or it can't be read.
    /// </summary>
    public static GladionValueCalibration Load(string path)
    {
        if (!File.Exists(path))
        {
            return Identity;
        }

        try
        {
            return JsonSerializer.Deserialize<GladionValueCalibration>(File.ReadAllText(path)) ?? Identity;
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            Log.Warning(exception, "Could not read the value calibration at {Path}", path);
            return Identity;
        }
    }
}
