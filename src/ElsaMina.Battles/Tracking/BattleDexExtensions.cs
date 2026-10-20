using ElsaMina.Core.Services.Dex;

namespace ElsaMina.Battles.Tracking;

public static class BattleDexExtensions
{
    private const string Z_PREFIX = "z";
    private const string MAX_PREFIX = "max";
    private const string GIGANTAMAX = "Gigantamax";
    private const int PLACEHOLDER_MOVE_PP = 1;

    /// <summary>
    /// poke-env's Move.entry: the data of a move, of the base move for a Z-move id ("zthunderbolt"), and a
    /// placeholder for "recharge" and "fight", which have no entry.
    /// </summary>
    public static bool TryGetMoveEntry(this IBattleDex dex, string moveId, out MoveData move)
    {
        if (dex.TryGetMove(moveId, out move))
        {
            return true;
        }

        if (moveId.StartsWith(Z_PREFIX, StringComparison.Ordinal) && dex.TryGetMove(moveId[1..], out move))
        {
            return true;
        }

        if (moveId is "recharge" or "fight")
        {
            move = new MoveData { Pp = PLACEHOLDER_MOVE_PP, Type = "normal", Category = "Special", Accuracy = 1 };
            return true;
        }

        return false;
    }

    /// <summary>
    /// A new <see cref="TrackedMove"/> with full PP. Throws, like poke-env, when the move has no data.
    /// </summary>
    public static TrackedMove CreateMove(this IBattleDex dex, string moveId, bool fromTransform = false,
        bool fromMimic = false)
    {
        if (!dex.TryGetMoveEntry(moveId, out var move))
        {
            throw new BattleTrackingException($"Unknown move: {moveId}");
        }

        return new TrackedMove(moveId, move.Pp, fromTransform, fromMimic);
    }

    /// <summary>
    /// poke-env's Move.should_be_stored: whether a move used or revealed joins the Pokémon's move set. Struggle,
    /// recharge, Z-moves and Max Moves don't.
    /// </summary>
    public static bool ShouldStoreMove(this IBattleDex dex, string moveId)
    {
        if (PokeEnvNames.SpecialMoves.Contains(moveId) || !dex.TryGetMove(moveId, out var move))
        {
            return false;
        }

        var isZMove = (moveId.StartsWith(Z_PREFIX, StringComparison.Ordinal) && dex.TryGetMove(moveId[1..], out _))
                      || !string.IsNullOrEmpty(move.IsZ);
        var isMaxMove = moveId.StartsWith(MAX_PREFIX, StringComparison.Ordinal)
                        || move.IsNonstandard == GIGANTAMAX
                        || move.IsMax;
        return !isZMove && !isMaxMove;
    }
}
