namespace ElsaMina.Battles.Tracking;

/// <summary>
/// A move a tracked Pokémon is known to have, with poke-env's PP count: it starts full and drops by one each time
/// the move is used (two against Pressure), whatever the request says.
/// </summary>
public sealed class TrackedMove
{
    private const int TRANSFORM_MAX_PP = 5;

    public TrackedMove(string id, int basePp, bool fromTransform = false, bool fromMimic = false)
    {
        Id = id;
        FromMimic = fromMimic;
        // poke-env's Move.max_pp: pp * 8 // 5 (PP Ups), capped at 5 for moves copied by Transform
        var maxPp = basePp * 8 / 5;
        MaxPp = fromTransform ? Math.Min(TRANSFORM_MAX_PP, maxPp) : maxPp;
        CurrentPp = MaxPp;
    }

    public string Id { get; }
    public bool FromMimic { get; }
    public int MaxPp { get; }
    public int CurrentPp { get; set; }

    /// <summary>
    /// poke-env's Move._is_last_used: the last move its Pokémon used since it switched in (Pokemon.last_move).
    /// </summary>
    public bool IsLastUsed { get; set; }

    public void Use(bool pressure = false, bool overridden = false)
    {
        var decrement = 1;
        if (pressure)
        {
            decrement++;
        }

        if (overridden)
        {
            decrement--;
        }

        CurrentPp = Math.Max(CurrentPp - decrement, 0);
    }
}
