using System.Text.Json;
using System.Text.Json.Serialization;
using ElsaMina.Battles.Tracking;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// encoder_spec.json, written by minipoke's export_onnx.py next to the model: everything the encoder must
/// reproduce from encoder.py (vocabularies, item hashing, one-hot orderings, feature sizes). Checks what every
/// model version shares; each version's encoder checks the feature sizes it writes.
/// </summary>
public sealed class EncoderSpec
{
    /// <summary>
    /// The only item hashing encoder.py implements: the encoder refuses a spec announcing another one.
    /// </summary>
    public const string ITEM_HASH_ALGORITHM = "crc32(utf8(item_id)) % buckets + 2";

    private static readonly string[] STAT_NAMES = ["hp", "atk", "def", "spa", "spd", "spe"];

    [JsonPropertyName("n_mons")]
    public int MonCount { get; set; }

    [JsonPropertyName("n_slots")]
    public int SlotCount { get; set; }

    [JsonPropertyName("n_moves")]
    public int MoveCount { get; set; }

    [JsonPropertyName("act_dim")]
    public int ActionCount { get; set; }

    [JsonPropertyName("f_mon")]
    public int MonFeatureSize { get; set; }

    [JsonPropertyName("f_move")]
    public int MoveFeatureSize { get; set; }

    [JsonPropertyName("f_field")]
    public int FieldFeatureSize { get; set; }

    [JsonPropertyName("pad_id")]
    public long PadId { get; set; }

    [JsonPropertyName("unk_id")]
    public long UnknownId { get; set; }

    [JsonPropertyName("item_hash")]
    public EncoderSpecItemHash ItemHash { get; set; } = new();

    [JsonPropertyName("vocab")]
    public EncoderSpecVocabularies Vocabularies { get; set; } = new();

    [JsonPropertyName("orderings")]
    public EncoderSpecOrderings Orderings { get; set; } = new();

    public static EncoderSpec Load(string path)
    {
        var spec = JsonSerializer.Deserialize<EncoderSpec>(File.ReadAllText(path))
                   ?? throw new InvalidOperationException($"Empty encoder spec at {path}");
        spec.Validate();
        return spec;
    }

    /// <summary>
    /// Fails loudly when the spec doesn't describe the slots, moves, actions and names the harness uses: a
    /// mismatch would not crash anything later, the model would just play badly.
    /// </summary>
    public void Validate()
    {
        var errors = new List<string>();
        CheckEqual(errors, "n_mons", MonCount, BattleObservation.MonCount);
        CheckEqual(errors, "n_slots", SlotCount, BattleObservation.SlotCount);
        CheckEqual(errors, "n_moves", MoveCount, BattleObservation.MoveCount);
        CheckEqual(errors, "act_dim", ActionCount, BattleObservation.ActionCount);

        // The tracker produces poke-env's names: every one must have a place in the spec, and vice versa
        var orderings = Orderings;
        CheckSameNames(errors, "types", orderings.Types, PokeEnvNames.Types);
        CheckSameNames(errors, "statuses", orderings.Statuses, PokeEnvNames.Statuses);
        CheckSameNames(errors, "weathers", orderings.Weathers, PokeEnvNames.Weathers);
        CheckSameNames(errors, "fields", orderings.Fields, PokeEnvNames.Fields);
        CheckSameNames(errors, "side_conditions", orderings.SideConditions, PokeEnvNames.SideConditions);
        CheckSameNames(errors, "categories", orderings.Categories, PokeEnvNames.Categories);
        CheckKnownNames(errors, "boosts", orderings.Boosts, PokeEnvNames.Boosts);
        CheckKnownNames(errors, "stats", orderings.Stats, STAT_NAMES);

        if (ItemHash.Algorithm != ITEM_HASH_ALGORITHM || ItemHash.Buckets <= 0)
        {
            errors.Add($"unsupported item hash '{ItemHash.Algorithm}' with {ItemHash.Buckets} buckets");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Encoder spec doesn't match the encoder: {string.Join("; ", errors)}");
        }
    }

    private static void CheckEqual(List<string> errors, string name, int actual, int expected)
    {
        if (actual != expected)
        {
            errors.Add($"{name} is {actual}, the encoder builds {expected}");
        }
    }

    private static void CheckSameNames(List<string> errors, string name, IReadOnlyCollection<string> specNames,
        IReadOnlyCollection<string> trackerNames)
    {
        if (!specNames.ToHashSet().SetEquals(trackerNames))
        {
            errors.Add($"{name} [{string.Join(", ", specNames)}] differ from poke-env's [{string.Join(", ", trackerNames)}]");
        }
    }

    private static void CheckKnownNames(List<string> errors, string name, IEnumerable<string> specNames,
        IReadOnlyCollection<string> knownNames)
    {
        var unknown = specNames.Where(specName => !knownNames.Contains(specName)).ToList();
        if (unknown.Count > 0)
        {
            errors.Add($"unknown {name}: {string.Join(", ", unknown)}");
        }
    }
}
