namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// What <see cref="MiniPokeActionGuard"/> reads from a battle's lines. Pokémon are known by their name in the
/// protocol idents ("p2a: Iron Valiant" -> "Iron Valiant"); lines by their index.
/// </summary>
public sealed class GuardHistory
{
    public GuardHistory(string ours, string opponent)
    {
        Ours = ours;
        Opponent = opponent;
    }

    public string Ours { get; }
    public string Opponent { get; }
    public string OurActive { get; private set; }
    public string OpponentActive { get; private set; }

    /// <summary>
    /// The last |switch|, |drag| or |replace| line of either side, -1 before any.
    /// </summary>
    public int LastEntranceLine { get; set; } = -1;

    /// <summary>
    /// The last |error| saying our active Pokémon is trapped, -1 without one.
    /// </summary>
    public int TrappedErrorLine { get; set; } = -1;

    /// <summary>
    /// (our Pokémon, move id, their Pokémon) -> the last |-immune| line after that chosen move.
    /// </summary>
    public Dictionary<(string User, string Move, string Target), int> ImmuneLines { get; } = [];

    /// <summary>
    /// (side, Pokémon) -> its |-terastallize| line.
    /// </summary>
    public Dictionary<(string Side, string Name), int> TeraLines { get; } = [];

    /// <summary>
    /// Hazard id -> layers on the opponent's side.
    /// </summary>
    public Dictionary<string, int> OpponentHazards { get; set; } = [];

    public void SetActive(string side, string name)
    {
        if (side == Ours)
        {
            OurActive = name;
        }
        else if (side == Opponent)
        {
            OpponentActive = name;
        }
    }
}
