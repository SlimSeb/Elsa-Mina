using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Core.Services.LanguageModel;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies.Llm;

public class LlmBattleDecisionService : IBattleDecisionService
{
    private readonly ILanguageModelProvider _languageModelProvider;
    private readonly IOpponentMovesPredictor _opponentMovesPredictor;
    private readonly ILlmBattlePromptBuilder _promptBuilder;
    private readonly ILlmBattleDecisionParser _decisionParser;
    private readonly IBattleDecisionService _fallbackDecisionService;

    public LlmBattleDecisionService(
        ILanguageModelProvider languageModelProvider,
        IOpponentMovesPredictor opponentMovesPredictor,
        ILlmBattlePromptBuilder promptBuilder,
        ILlmBattleDecisionParser decisionParser,
        CalcBasedBattleDecisionService fallbackDecisionService)
        : this(languageModelProvider, opponentMovesPredictor, promptBuilder, decisionParser, (IBattleDecisionService)fallbackDecisionService)
    {
    }

    public LlmBattleDecisionService(
        ILanguageModelProvider languageModelProvider,
        IOpponentMovesPredictor opponentMovesPredictor,
        ILlmBattlePromptBuilder promptBuilder,
        ILlmBattleDecisionParser decisionParser,
        IBattleDecisionService fallbackDecisionService)
    {
        _languageModelProvider = languageModelProvider;
        _opponentMovesPredictor = opponentMovesPredictor;
        _promptBuilder = promptBuilder;
        _decisionParser = decisionParser;
        _fallbackDecisionService = fallbackDecisionService;
    }

    public LlmBattleDecisionService(
        ILanguageModelProvider languageModelProvider,
        IOpponentMovesPredictor opponentMovesPredictor,
        IRandomService randomService,
        IBattleSearchAlgorithm searchAlgorithm = null,
        ILlmBattlePromptBuilder promptBuilder = null,
        ILlmBattleDecisionParser decisionParser = null)
        : this(
            languageModelProvider,
            opponentMovesPredictor,
            promptBuilder ?? new LlmBattlePromptBuilder(),
            decisionParser ?? new LlmBattleDecisionParser(),
            new CalcBasedBattleDecisionService(randomService, opponentMovesPredictor, searchAlgorithm ?? new MinimaxSearch()))
    {
    }

    public async Task<BattleDecision> GetDecisionAsync(BattleContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.IsBattleOver || context.Wait)
        {
            return null;
        }

        try
        {
            if (context.TeamPreview && context.SidePokemon.Count > 0)
            {
                return await GetTeamPreviewDecisionAsync(context, cancellationToken);
            }

            if (context.ForceSwitchSlots.Any(slot => slot))
            {
                return await GetForcedSwitchDecisionAsync(context, cancellationToken);
            }

            if (context.ActiveSlots.Count == 1)
            {
                return await GetSingleTurnDecisionAsync(context, cancellationToken);
            }

            if (context.ActiveSlots.Count > 1)
            {
                Log.Information("Doubles battle detected - delegating to fallback decision service");
                return await _fallbackDecisionService.GetDecisionAsync(context, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error in LLM battle decision service. Falling back to default strategy.");
            return await _fallbackDecisionService.GetDecisionAsync(context, cancellationToken);
        }

        return null;
    }

    private async Task<BattleDecision> GetTeamPreviewDecisionAsync(BattleContext context,
        CancellationToken cancellationToken)
    {
        var prediction = await PredictOpponentAsync(context, cancellationToken);
        var prompt = _promptBuilder.BuildTeamPreviewPrompt(context, prediction);
        var systemPrompt = _promptBuilder.BuildSystemPrompt();

        var request = new LanguageModelRequest
        {
            SystemPrompt = systemPrompt,
            InputConversation =
            [
                new LanguageModelMessage
                {
                    Role = MessageRole.User,
                    Content = prompt
                }
            ]
        };

        var response = await AskLlmSafelyAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(response))
        {
            var parsed = _decisionParser.Parse(response);
            if (parsed.IsValid && parsed.ChoiceIndex >= 1 && parsed.ChoiceIndex <= context.SidePokemon.Count)
            {
                var candidate = context.SidePokemon[parsed.ChoiceIndex - 1];
                if (!candidate.IsFainted)
                {
                    Log.Information("LLM chose lead Pokémon slot {0} with reasoning: {1}", parsed.ChoiceIndex, parsed.Reasoning);
                    return new BattleDecision(BattleDecisionType.TeamPreview, [parsed.ChoiceIndex]);
                }
            }

            Log.Warning("LLM team preview decision was invalid or illegal. Fallback to default strategy. Response: {0}", response);
        }

        return await _fallbackDecisionService.GetDecisionAsync(context, cancellationToken);
    }

    private async Task<BattleDecision> GetForcedSwitchDecisionAsync(BattleContext context,
        CancellationToken cancellationToken)
    {
        var candidates = GetSwitchCandidates(context);
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return new BattleDecision(BattleDecisionType.Switch, [candidates[0]]);
        }

        var switchSlotCount = context.ForceSwitchSlots.Count(slot => slot);
        if (switchSlotCount > 1)
        {
            return await _fallbackDecisionService.GetDecisionAsync(context, cancellationToken);
        }

        var prediction = await PredictOpponentAsync(context, cancellationToken);
        var prompt = _promptBuilder.BuildForcedSwitchPrompt(context, prediction, candidates);
        var systemPrompt = _promptBuilder.BuildSystemPrompt();

        var request = new LanguageModelRequest
        {
            SystemPrompt = systemPrompt,
            InputConversation =
            [
                new LanguageModelMessage
                {
                    Role = MessageRole.User,
                    Content = prompt
                }
            ]
        };

        var response = await AskLlmSafelyAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(response))
        {
            var parsed = _decisionParser.Parse(response);
            if (parsed.IsValid && candidates.Contains(parsed.ChoiceIndex))
            {
                Log.Information("LLM chose forced switch to slot {0} with reasoning: {1}", parsed.ChoiceIndex, parsed.Reasoning);
                return new BattleDecision(BattleDecisionType.Switch, [parsed.ChoiceIndex]);
            }

            Log.Warning("LLM forced switch decision was invalid or illegal. Fallback to default strategy. Response: {0}", response);
        }

        return await _fallbackDecisionService.GetDecisionAsync(context, cancellationToken);
    }

    private async Task<BattleDecision> GetSingleTurnDecisionAsync(BattleContext context,
        CancellationToken cancellationToken)
    {
        var activeSlot = context.ActiveSlots[0];
        var availableMoves = GetAvailableMoveIndices(activeSlot);
        var switchCandidates = GetSwitchCandidates(context);

        if (availableMoves.Count == 0 && switchCandidates.Count == 0)
        {
            return null;
        }

        var prediction = await PredictOpponentAsync(context, cancellationToken);
        var prompt = _promptBuilder.BuildTurnPrompt(context, prediction);
        var systemPrompt = _promptBuilder.BuildSystemPrompt();

        var request = new LanguageModelRequest
        {
            SystemPrompt = systemPrompt,
            InputConversation =
            [
                new LanguageModelMessage
                {
                    Role = MessageRole.User,
                    Content = prompt
                }
            ]
        };

        var response = await AskLlmSafelyAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(response))
        {
            var parsed = _decisionParser.Parse(response);
            if (parsed.IsValid)
            {
                if (parsed.DecisionType == BattleDecisionType.Move && availableMoves.Contains(parsed.ChoiceIndex))
                {
                    var canTera = !string.IsNullOrEmpty(activeSlot.CanTerastallize) && parsed.UseTerastallize;
                    Log.Information("LLM chose move {0} (Tera: {1}) with reasoning: {2}", parsed.ChoiceIndex, canTera, parsed.Reasoning);
                    return new BattleDecision(BattleDecisionType.Move, [parsed.ChoiceIndex], canTera);
                }

                if (parsed.DecisionType == BattleDecisionType.Switch && !activeSlot.Trapped && switchCandidates.Contains(parsed.ChoiceIndex))
                {
                    Log.Information("LLM chose switch to slot {0} with reasoning: {1}", parsed.ChoiceIndex, parsed.Reasoning);
                    return new BattleDecision(BattleDecisionType.Switch, [parsed.ChoiceIndex]);
                }
            }

            Log.Warning("LLM turn decision was invalid or illegal. Fallback to default strategy. Response: {0}", response);
        }

        return await _fallbackDecisionService.GetDecisionAsync(context, cancellationToken);
    }

    private async Task<OpponentPrediction> PredictOpponentAsync(BattleContext context,
        CancellationToken cancellationToken)
    {
        var opponent = context.ActiveOpponent;
        if (opponent == null)
        {
            return OpponentPrediction.Empty;
        }

        try
        {
            return await _opponentMovesPredictor.PredictAsync(
                context.Format,
                opponent.Species,
                opponent.RevealedMoves,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Debug("Opponent prediction failed: {Message}", ex.Message);
            return OpponentPrediction.Empty;
        }
    }

    private async Task<string> AskLlmSafelyAsync(LanguageModelRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _languageModelProvider.AskLanguageModelAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calling language model for battle decision");
            return null;
        }
    }

    private static List<int> GetSwitchCandidates(BattleContext context)
    {
        var candidates = new List<int>();
        for (var index = 0; index < context.SidePokemon.Count; index++)
        {
            var pokemon = context.SidePokemon[index];
            if (!pokemon.IsActive && !pokemon.IsFainted && pokemon.CurrentHp > 0)
            {
                candidates.Add(index + 1);
            }
        }

        return candidates;
    }

    private static List<int> GetAvailableMoveIndices(BattleActiveSlot slot)
    {
        var available = new List<int>();
        for (var index = 0; index < slot.Moves.Count; index++)
        {
            var move = slot.Moves[index];
            if (move.Name == "Recharge" || move.MaxPp == 0 || (!move.IsDisabled && move.Pp > 0))
            {
                available.Add(index + 1);
            }
        }

        return available;
    }
}
