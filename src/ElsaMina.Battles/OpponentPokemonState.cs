namespace ElsaMina.Battles;

public class OpponentPokemonState
{
    public string Species { get; set; } = "";
    public int Level { get; set; } = 100;
    public string Gender { get; set; } = "";
    public double HpPercent { get; set; } = 100.0;
    public string Status { get; set; } = "";
    public bool IsFainted { get; set; }
    public bool IsActive { get; set; }
    public string LastUsedMove { get; set; } = "";
    public Dictionary<string, int> Boosts { get; set; } = new();
}
