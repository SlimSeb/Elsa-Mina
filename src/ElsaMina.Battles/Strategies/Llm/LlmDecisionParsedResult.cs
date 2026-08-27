namespace ElsaMina.Battles.Strategies.Llm;

public class LlmDecisionParsedResult
{
    public bool IsValid { get; init; }
    public BattleDecisionType? DecisionType { get; init; }
    public int ChoiceIndex { get; init; }
    public bool UseTerastallize { get; init; }
    public string Reasoning { get; init; } = "";

    public static LlmDecisionParsedResult Invalid(string reasoning = "") => new()
    {
        IsValid = false,
        Reasoning = reasoning
    };

    public static LlmDecisionParsedResult Valid(BattleDecisionType type, int index, bool terastallize = false, string reasoning = "") => new()
    {
        IsValid = true,
        DecisionType = type,
        ChoiceIndex = index,
        UseTerastallize = terastallize,
        Reasoning = reasoning
    };
}
