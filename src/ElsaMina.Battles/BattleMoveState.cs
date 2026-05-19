namespace ElsaMina.Battles;

public class BattleMoveState
{
    public string Name { get; init; } = "";
    public string Id { get; init; } = "";
    public int Pp { get; init; }
    public int MaxPp { get; init; }
    public string Target { get; init; } = "";
    public bool IsDisabled { get; init; }
    public string Type { get; set; } = "";
}
