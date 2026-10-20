namespace ElsaMina.Battles.Tracking;

/// <summary>
/// poke-env's MoveSet: a Pokémon's moves in the order they were first seen, with the moves copied by Transform
/// replacing them while it lasts, and the move copied by Mimic taking Mimic's place.
/// </summary>
public sealed class TrackedMoveSet
{
    private const string MIMIC = "mimic";

    private readonly OrderedDictionary<string, TrackedMove> _baseMoves = new();
    private TrackedMove _mimicMove;

    /// <summary>
    /// The moves copied by Transform, or null when not transformed.
    /// </summary>
    public TrackedMoveSet TransformMoves { get; set; }

    /// <summary>
    /// The moves in use, Transform applied, without the Mimic substitution.
    /// </summary>
    public OrderedDictionary<string, TrackedMove> BaseMoves => Resolve()._baseMoves;

    public TrackedMove MimicMove
    {
        get => Resolve()._mimicMove;
        set => Resolve()._mimicMove = value;
    }

    /// <summary>
    /// The moves in use: Transform applied, and the Mimic-copied move in place of Mimic.
    /// </summary>
    public OrderedDictionary<string, TrackedMove> Moves
    {
        get
        {
            var mimicMove = MimicMove;
            if (mimicMove == null)
            {
                return BaseMoves;
            }

            var moves = new OrderedDictionary<string, TrackedMove>();
            foreach (var (id, move) in BaseMoves)
            {
                if (id == MIMIC)
                {
                    moves[mimicMove.Id] = mimicMove;
                }
                else
                {
                    moves[id] = move;
                }
            }

            return moves;
        }
    }

    private TrackedMoveSet Resolve() => TransformMoves?.Resolve() ?? this;
}
