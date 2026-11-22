using System.Text.Json.Nodes;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPoke;

/// <summary>
/// A minipoke v1 spec (encoder.py's, which names no encoder_version), for the tests of what V1 specs and the V1
/// encoder do: no V1 model is released anymore, and a V2 spec is exactly V1's plus the entries removed here.
/// </summary>
public static class MiniPokeV1Spec
{
    private static readonly string V2_SPEC_PATH =
        Path.Join(AppContext.BaseDirectory, "Models", "MiniPokeV3_22M", "encoder_spec.json");
    private static readonly string[] V2_ENTRIES =
        ["encoder_version", "history", "inputs", "model", "move_effects", "set_priors"];
    private static readonly string[] V2_ORDERINGS = ["cand_feats", "effect_feats", "hist_feats", "prior_feats"];

    /// <summary>
    /// The released MiniPokeV3_22M spec (V2.1's observation) without its V2 entries.
    /// </summary>
    public static JsonObject Create()
    {
        var spec = JsonNode.Parse(File.ReadAllText(V2_SPEC_PATH)).AsObject();
        foreach (var entry in V2_ENTRIES)
        {
            spec.Remove(entry);
        }

        var orderings = spec["orderings"].AsObject();
        foreach (var ordering in V2_ORDERINGS)
        {
            orderings.Remove(ordering);
        }

        return spec;
    }

    /// <summary>
    /// Writes the V1 spec to <paramref name="directory"/>/encoder_spec.json, for an encoder reading a model directory.
    /// </summary>
    public static void WriteTo(string directory) =>
        File.WriteAllText(Path.Join(directory, "encoder_spec.json"), Create().ToJsonString());
}
