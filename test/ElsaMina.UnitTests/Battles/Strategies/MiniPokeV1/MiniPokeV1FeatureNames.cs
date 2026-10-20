using ElsaMina.Battles.Strategies.MiniPoke;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV1;

/// <summary>
/// Readable names for every position of encoder.py's feature vectors, so a parity failure says which feature
/// differs. Built from encoder_spec.json in encoder.py's layout.
/// </summary>
public class MiniPokeV1FeatureNames
{
    private static readonly string[] ID_KINDS = ["species", "item", "ability"];

    private static readonly string[] ACTIONS =
    [
        "switch 0", "switch 1", "switch 2", "switch 3", "switch 4", "switch 5",
        "move 0", "move 1", "move 2", "move 3", "tera move 0", "tera move 1", "tera move 2", "tera move 3"
    ];

    public MiniPokeV1FeatureNames(EncoderSpec spec)
    {
        var orderings = spec.Orderings;
        Mon =
        [
            "present", "revealed_placeholder", "is_mine", "active", "fainted", "hp",
            ..orderings.Statuses.Select(status => $"status:{status}"),
            ..orderings.Boosts.Select(boost => $"boost:{boost}"),
            ..orderings.Types.Select(type => $"type:{type}"),
            ..orderings.Types.Select(type => $"tera_type:{type}"),
            "terastallized",
            ..orderings.Stats.Select(stat => $"base_stat:{stat}"),
            "level", "item_known", "ability_known",
            ..orderings.Effects.Select(effect => $"effect:{effect}"),
            "must_recharge", "first_turn", "preparing", "known_moves"
        ];
        Move =
        [
            "present", "base_power", "accuracy", "pp", "priority",
            ..orderings.Categories.Select(category => $"category:{category}"),
            ..orderings.Types.Select(type => $"type:{type}"),
            "effectiveness", "immune", "stab", "heal", "drain", "recoil", "protect", "self_switch",
            "side_condition", "status", "force_switch"
        ];
        Field =
        [
            ..orderings.Weathers.Select(weather => $"weather:{weather}"),
            ..orderings.Fields.Select(field => $"field:{field}"),
            ..orderings.SideConditions.Select(condition => $"our_side:{condition}"),
            ..orderings.SideConditions.Select(condition => $"opponent_side:{condition}"),
            "turn", "can_tera", "used_tera", "opponent_used_tera", "force_switch", "trapped", "our_alive",
            "opponent_alive", "opponent_revealed"
        ];
    }

    public IReadOnlyList<string> Mon { get; }
    public IReadOnlyList<string> Move { get; }
    public IReadOnlyList<string> Field { get; }

    public string Describe(string array, int index)
    {
        const int moves = BattleObservation.MoveCount;
        return array switch
        {
            "mon_ids" => $"mon {index / 3} {ID_KINDS[index % 3]}",
            "move_ids" => $"mon {index / moves} move {index % moves}",
            "mon_feats" => $"mon {index / Mon.Count} {Mon[index % Mon.Count]}",
            "move_feats" => $"mon {index / (moves * Move.Count)} move {index / Move.Count % moves} {Move[index % Move.Count]}",
            "field" => Field[index],
            "mask" => ACTIONS[index],
            _ => index.ToString()
        };
    }
}
