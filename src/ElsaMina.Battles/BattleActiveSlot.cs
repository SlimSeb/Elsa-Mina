namespace ElsaMina.Battles;

public class BattleActiveSlot
{
    public List<BattleMoveState> Moves { get; init; } = [];
    public string CanTerastallize { get; init; } = "";
    public bool Trapped { get; init; }
}
