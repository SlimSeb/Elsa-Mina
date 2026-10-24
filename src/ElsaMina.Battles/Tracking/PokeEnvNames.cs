namespace ElsaMina.Battles.Tracking;

/// <summary>
/// The enumerations of poke-env 0.16.1 (Weather, Field, SideCondition, Status, PokemonType, MoveCategory, Effect),
/// as the upper-case member names poke-env uses, and its parsing of Showdown messages into them. The tracker
/// stores these names; the minipoke encoders map them to one-hot positions using encoder_spec.json and check that
/// both sides agree on the names.
/// </summary>
public static class PokeEnvNames
{
    public const string UNKNOWN = "UNKNOWN";
    public const string FAINTED = "FNT";
    public const string SLEEP = "SLP";
    public const string TOXIC = "TOX";
    public const string STELLAR = "STELLAR";
    public const string THREE_QUESTION_MARKS = "THREE_QUESTION_MARKS";
    public const string NEUTRALIZING_GAS = "NEUTRALIZING_GAS";
    public const string TERRAIN_SUFFIX = "_TERRAIN";

    public static readonly IReadOnlyList<string> Statuses = ["BRN", "FNT", "FRZ", "PAR", "PSN", "SLP", "TOX"];

    public static readonly IReadOnlyList<string> Types =
    [
        "BUG", "DARK", "DRAGON", "ELECTRIC", "FAIRY", "FIGHTING", "FIRE", "FLYING", "GHOST", "GRASS", "GROUND",
        "ICE", "NORMAL", "POISON", "PSYCHIC", "ROCK", "STEEL", "WATER", THREE_QUESTION_MARKS, STELLAR
    ];

    public static readonly IReadOnlyList<string> Categories = ["PHYSICAL", "SPECIAL", "STATUS"];

    public static readonly IReadOnlyList<string> Weathers =
    [
        UNKNOWN, "DESOLATELAND", "DELTASTREAM", "HAIL", "PRIMORDIALSEA", "RAINDANCE", "SANDSTORM", "SNOWSCAPE",
        "SUNNYDAY"
    ];

    public static readonly IReadOnlyList<string> Fields =
    [
        UNKNOWN, "ELECTRIC_TERRAIN", "FAIRY_LOCK", "GRASSY_TERRAIN", "GRAVITY", "HEAL_BLOCK", "MAGIC_ROOM",
        "MISTY_TERRAIN", "MUD_SPORT", "MUD_SPOT", NEUTRALIZING_GAS, "PSYCHIC_TERRAIN", "TRICK_ROOM", "WATER_SPORT",
        "WONDER_ROOM"
    ];

    public static readonly IReadOnlyList<string> SideConditions =
    [
        UNKNOWN, "AURORA_VEIL", "CRAFTY_SHIELD", "FIRE_PLEDGE", "G_MAX_CANNONADE", "G_MAX_STEELSURGE",
        "G_MAX_VINE_LASH", "G_MAX_VOLCALITH", "G_MAX_WILDFIRE", "GRASS_PLEDGE", "LIGHT_SCREEN", "LUCKY_CHANT",
        "MATBLOCK", "MIST", "QUICK_GUARD", "REFLECT", "SAFEGUARD", "SPIKES", "STEALTH_ROCK", "STICKY_WEB", "TAILWIND",
        "TOXIC_SPIKES", "WATER_PLEDGE", "WIDE_GUARD"
    ];

    /// <summary>
    /// Side conditions that stack, with their maximum useful number of layers (poke-env's STACKABLE_CONDITIONS).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> StackableSideConditions =
        new Dictionary<string, int> { ["SPIKES"] = 3, ["TOXIC_SPIKES"] = 2 };

    /// <summary>
    /// Stat stages in poke-env's Pokemon.boosts order.
    /// </summary>
    public static readonly IReadOnlyList<string> Boosts = ["accuracy", "atk", "def", "evasion", "spa", "spd", "spe"];

    /// <summary>
    /// Moves a Pokémon never stores in its move set (poke-env's SPECIAL_MOVES).
    /// </summary>
    public static readonly IReadOnlySet<string> SpecialMoves = new HashSet<string> { "struggle", "recharge", "fight" };

    /// <summary>
    /// poke-env's _PROTECT_MOVES (Move.is_protect_move).
    /// </summary>
    public static readonly IReadOnlySet<string> ProtectMoves = new HashSet<string>
    {
        "protect", "detect", "endure", "spikyshield", "kingsshield", "banefulbunker", "burningbulwark", "obstruct",
        "maxguard", "silktrap"
    };

    /// <summary>
    /// poke-env's _PROTECT_COUNTER_MOVES (Move.is_protect_counter): the moves that raise Pokemon.protect_counter.
    /// </summary>
    public static readonly IReadOnlySet<string> ProtectCounterMoves =
        new HashSet<string>(ProtectMoves) { "wideguard", "quickguard" };

    /// <summary>
    /// poke-env's _PROTECT_BREAKING_EFFECTS (Effect.breaks_protect): starting one resets the protect counter.
    /// </summary>
    public static readonly IReadOnlySet<string> ProtectBreakingEffects = new HashSet<string>
    {
        "FEINT", "G_MAX_ONE_BLOW", "G_MAX_RAPID_FLOW", "SHADOW_FORCE", "PHANTOM_FORCE", "HYPERSPACE_FURY",
        "HYPERSPACE_HOLE"
    };

    /// <summary>
    /// Effects removed at the end of each turn (poke-env's _ENDS_ON_TURN_EFFECTS).
    /// </summary>
    public static readonly IReadOnlySet<string> EffectsEndingOnTurn = new HashSet<string>
    {
        "AFTER_YOU", "BANEFUL_BUNKER", "BEAK_BLAST", "BURNING_BULWARK", "CRAFTY_SHIELD", "FEINT", "FLINCH",
        "FOCUS_PUNCH", "FOLLOW_ME", "KINGS_SHIELD", "CUSTAP_BERRY", "MIND_READER", "MAGIC_COAT", "OBSTRUCT",
        "PROTECT", "QUASH", "QUICK_CLAW", "QUICK_DRAW", "QUICK_GUARD", "RAGE_POWDER", "ROOST", "SPIKY_SHIELD",
        "SPOTLIGHT", "WIDE_GUARD", "INSTRUCT"
    };

    public static readonly IReadOnlyList<string> QuarkDriveEffects =
        ["QUARKDRIVEATK", "QUARKDRIVEDEF", "QUARKDRIVESPA", "QUARKDRIVESPD", "QUARKDRIVESPE"];

    public static readonly IReadOnlyList<string> ProtosynthesisEffects =
        ["PROTOSYNTHESISATK", "PROTOSYNTHESISDEF", "PROTOSYNTHESISSPA", "PROTOSYNTHESISSPD", "PROTOSYNTHESISSPE"];

    /// <summary>
    /// PokemonType.from_name: "???" or a type name in any case.
    /// </summary>
    public static string TypeFromName(string name)
    {
        var type = name == "???" ? THREE_QUESTION_MARKS : name.ToUpperInvariant();
        return Types.Contains(type) ? type : throw new BattleTrackingException($"Unknown type '{name}'");
    }

    /// <summary>
    /// Status[name.upper()].
    /// </summary>
    public static string StatusFromName(string name)
    {
        var status = name.ToUpperInvariant();
        return Statuses.Contains(status) ? status : throw new BattleTrackingException($"Unknown status '{name}'");
    }

    /// <summary>
    /// Effect.from_showdown_message. Names that are not poke-env effects are kept as they are (poke-env maps them
    /// to Effect.UNKNOWN); no feature reads them.
    /// </summary>
    public static string EffectFromMessage(string message)
    {
        var effect = message
            .Replace("item: ", "")
            .Replace("move: ", "")
            .Replace("ability: ", "")
            .Replace(" ", "_")
            .Replace("-", "_")
            .ToUpperInvariant();
        return effect == "FALLENUNDEFINED" ? "FALLEN" : effect;
    }

    /// <summary>
    /// Weather.from_showdown_message. SNOW is an alias of SNOWSCAPE in poke-env's enum.
    /// </summary>
    public static string WeatherFromMessage(string message)
    {
        var weather = message.Replace("move: ", "").Replace(" ", "_").Replace("-", "_").ToUpperInvariant();
        if (weather == "SNOW")
        {
            return "SNOWSCAPE";
        }

        return Weathers.Contains(weather) ? weather : UNKNOWN;
    }

    /// <summary>
    /// Field.from_showdown_message.
    /// </summary>
    public static string FieldFromMessage(string message)
    {
        var field = message.Replace("ability: ", "").Replace("move: ", "").Replace(" ", "_");
        if (field.EndsWith("terrain", StringComparison.Ordinal) && !field.EndsWith("_terrain", StringComparison.Ordinal))
        {
            field = field.Replace("terrain", "_terrain");
        }

        field = field.ToUpperInvariant();
        return Fields.Contains(field) ? field : UNKNOWN;
    }

    /// <summary>
    /// SideCondition.from_showdown_message.
    /// </summary>
    public static string SideConditionFromMessage(string message)
    {
        var condition = message.Replace("move: ", "").Replace(" ", "_").Replace("-", "_").ToUpperInvariant();
        return SideConditions.Contains(condition) ? condition : UNKNOWN;
    }

    public static bool IsTerrain(string field) => field.EndsWith(TERRAIN_SUFFIX, StringComparison.Ordinal);
}
