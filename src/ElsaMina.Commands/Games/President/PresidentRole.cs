namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Social rank earned by the finish order of a round. With fewer than 4 players only
/// <see cref="President"/> and <see cref="Scum"/> are assigned.
/// </summary>
public enum PresidentRole
{
    None,
    President,
    VicePresident,
    Neutral,
    ViceScum,
    Scum
}
