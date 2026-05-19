namespace ElsaMina.Battles;

public class BattleDecision
{
    public BattleDecision(BattleDecisionType type, List<int> choices, bool useTerastallize = false)
    {
        Type = type;
        Choices = choices;
        UseTerastallize = useTerastallize;
    }

    public BattleDecisionType Type { get; }
    public List<int> Choices { get; }
    public bool UseTerastallize { get; }
}
