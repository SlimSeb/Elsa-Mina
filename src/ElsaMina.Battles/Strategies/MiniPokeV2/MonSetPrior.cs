namespace ElsaMina.Battles.Strategies.MiniPokeV2;

/// <summary>
/// What the set priors say about one opponent Pokémon (setpriors.py's MonPrior).
/// </summary>
public sealed class MonSetPrior
{
    /// <summary>
    /// A species without random battle sets: every feature stays 0.
    /// </summary>
    public static readonly MonSetPrior NotFound = new();

    public bool Found { get; init; }
    public int RoleCount { get; init; }
    public int ConsistentRoleCount { get; init; }

    /// <summary>
    /// No role contains every piece of evidence: every role of the species is used.
    /// </summary>
    public bool Fallback { get; init; }

    /// <summary>
    /// Moves not revealed yet, with the fraction of the roles used that have them, most likely first.
    /// </summary>
    public IReadOnlyList<SetPriorWeight> Candidates { get; init; } = [];

    /// <summary>
    /// Abilities by weight, highest first; the revealed one alone, with weight 1.
    /// </summary>
    public IReadOnlyList<SetPriorWeight> Abilities { get; init; } = [];

    public IReadOnlyList<SetPriorWeight> Items { get; init; } = [];
    public IReadOnlyList<SetPriorWeight> TeraTypes { get; init; } = [];

    /// <summary>
    /// It may hold a Choice item (the move lock feature reads it).
    /// </summary>
    public bool ChoicePossible { get; init; }
}
