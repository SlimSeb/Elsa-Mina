using ElsaMina.Battles.Strategies.Prediction;

namespace ElsaMina.Battles.Strategies.Llm;

public interface ILlmBattlePromptBuilder
{
    string BuildSystemPrompt();
    string BuildTeamPreviewPrompt(BattleContext context, OpponentPrediction prediction);
    string BuildForcedSwitchPrompt(BattleContext context, OpponentPrediction prediction, IReadOnlyList<int> candidateIndices);
    string BuildTurnPrompt(BattleContext context, OpponentPrediction prediction);
}
