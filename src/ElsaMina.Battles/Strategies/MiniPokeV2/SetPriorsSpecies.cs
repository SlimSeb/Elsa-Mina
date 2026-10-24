namespace ElsaMina.Battles.Strategies.MiniPokeV2;

/// <summary>
/// One species' roles, and its pools: every move, ability, item and tera type any of its roles has.
/// </summary>
public sealed class SetPriorsSpecies
{
    public SetPriorsSpecies(IReadOnlyList<SetPriorsRole> roles)
    {
        Roles = roles;
        MovePool = roles.SelectMany(role => role.Moves).ToHashSet();
        AbilityPool = roles.SelectMany(role => role.Abilities).ToHashSet();
        ItemPool = roles.SelectMany(role => role.Items).ToHashSet();
        TeraTypePool = roles.SelectMany(role => role.TeraTypes).ToHashSet();
    }

    public IReadOnlyList<SetPriorsRole> Roles { get; }
    public IReadOnlySet<string> MovePool { get; }
    public IReadOnlySet<string> AbilityPool { get; }
    public IReadOnlySet<string> ItemPool { get; }
    public IReadOnlySet<string> TeraTypePool { get; }
}
