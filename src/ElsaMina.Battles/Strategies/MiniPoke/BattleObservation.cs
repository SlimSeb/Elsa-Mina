namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// One encoded battle state, laid out exactly like minipoke's Python encoder
/// (see encoder_spec.json produced by export_onnx.py). Arrays are flat, row-major.
/// </summary>
public sealed class BattleObservation
{
    public const int MonCount = 12;
    public const int SlotCount = 6;
    public const int MoveCount = 4;
    public const int ActionCount = 14;

    /// <summary>
    /// The inputs every model version reads, in the ONNX model's order.
    /// </summary>
    public static readonly IReadOnlyList<string> BaseInputNames =
        ["mon_ids", "move_ids", "mon_feats", "move_feats", "field", "mask"];

    public BattleObservation(int monFeatureSize, int moveFeatureSize, int fieldFeatureSize)
    {
        MonFeatureSize = monFeatureSize;
        MoveFeatureSize = moveFeatureSize;
        MonIds = new long[MonCount * 3];
        MoveIds = new long[MonCount * MoveCount];
        MonFeatures = new float[MonCount * monFeatureSize];
        MoveFeatures = new float[MonCount * MoveCount * moveFeatureSize];
        Field = new float[fieldFeatureSize];
        Mask = new bool[ActionCount];
        TeamSlotToSideIndex = Enumerable.Repeat(-1, SlotCount).ToArray();
        MoveSlotToRequestIndex = Enumerable.Repeat(-1, MoveCount).ToArray();
        MoveSlotIds = new string[MoveCount];
    }

    public int MonFeatureSize { get; }
    public int MoveFeatureSize { get; }

    /// <summary>[12, 3]: species, item, ability ids.</summary>
    public long[] MonIds { get; }

    /// <summary>[12, 4]</summary>
    public long[] MoveIds { get; }

    /// <summary>[12, F_MON]</summary>
    public float[] MonFeatures { get; }

    /// <summary>[12, 4, F_MOVE]</summary>
    public float[] MoveFeatures { get; }

    /// <summary>[F_FIELD]</summary>
    public float[] Field { get; }

    /// <summary>[14]: legal actions (0-5 switch, 6-9 move, 10-13 tera + move).</summary>
    public bool[] Mask { get; }

    /// <summary>
    /// The int64 inputs a model version adds after the six above (minipoke-v2's cand_ids, prior_ids...), by ONNX
    /// input name. Their shapes are the model's.
    /// </summary>
    public Dictionary<string, long[]> ExtraIds { get; } = [];

    /// <summary>
    /// The float32 inputs a model version adds after the six above (minipoke-v2's cand_feats, hist_feats...).
    /// </summary>
    public Dictionary<string, float[]> ExtraFeatures { get; } = [];

    /// <summary>
    /// Maps the model's team slot (stable, first-seen order, like poke-env's battle.team)
    /// to the index of that Pokémon in the *current* <see cref="BattleContext.SidePokemon"/>,
    /// which Showdown reorders on every switch. -1 when the slot is empty.
    /// </summary>
    public int[] TeamSlotToSideIndex { get; }

    /// <summary>
    /// Maps the model's move j to the index of that move in the request's active moves
    /// (<see cref="BattleActiveSlot.Moves"/>), which is what /choose move uses. They differ when the
    /// request lists fewer moves (locked into Outrage, Struggle, recharge). -1 when not mappable.
    /// </summary>
    public int[] MoveSlotToRequestIndex { get; }

    /// <summary>
    /// The id of the move the model's move j stands for, null when there is none.
    /// </summary>
    public string[] MoveSlotIds { get; }
}
