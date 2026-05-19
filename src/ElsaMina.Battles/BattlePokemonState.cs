namespace ElsaMina.Battles;

public class BattlePokemonState
{
    public string Ident { get; init; } = "";
    public string Details { get; init; } = "";
    public string Condition { get; init; } = "";
    public int CurrentHp { get; init; }
    public int MaxHp { get; init; }
    public bool IsActive { get; init; }
    public bool IsFainted { get; init; }
    public BattlePokemonStats Stats { get; init; } = new(0, 0, 0, 0, 0);
    public List<string> Moves { get; init; } = [];
    public string BaseAbility { get; init; } = "";
    public string Ability { get; init; } = "";
    public string Item { get; init; } = "";
    public string Pokeball { get; init; } = "";
    public string TeraType { get; init; } = "";
    public string Terastallized { get; init; } = "";
    public bool Commanding { get; init; }
    public bool Reviving { get; init; }
}
