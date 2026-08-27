namespace ElsaMina.Battles.Strategies.Llm;

public interface ILlmBattleDecisionParser
{
    LlmDecisionParsedResult Parse(string response);
}
