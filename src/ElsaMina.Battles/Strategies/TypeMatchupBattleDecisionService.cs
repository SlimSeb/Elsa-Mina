using ElsaMina.Battles.Data;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Utils;

namespace ElsaMina.Battles.Strategies;

public class TypeMatchupBattleDecisionService : IBattleDecisionService
{
    private readonly IRandomService _randomService;

    public TypeMatchupBattleDecisionService(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public bool TryGetDecision(BattleContext context, out BattleDecision decision)
    {
        decision = null;
        if (context.IsBattleOver)
        {
            return false;
        }

        if (context.TeamPreview && context.SidePokemon.Count > 0)
        {
            var choice = _randomService.NextInt(1, context.SidePokemon.Count + 1);
            decision = new BattleDecision(BattleDecisionType.TeamPreview, [choice]);
            return true;
        }

        if (context.ForceSwitchSlots.Any(slot => slot))
        {
            var choices = BuildSwitchChoices(context);
            if (choices.Count == 0)
            {
                return false;
            }

            decision = new BattleDecision(BattleDecisionType.Switch, choices);
            return true;
        }

        if (context.ActiveSlots.Count > 0)
        {
            var choices = BuildMoveChoices(context);
            if (choices.Count == 0)
            {
                return false;
            }

            decision = new BattleDecision(BattleDecisionType.Move, choices);
            return true;
        }

        return false;
    }

    private List<int> BuildSwitchChoices(BattleContext context)
    {
        var candidates = GetSwitchCandidates(context);
        if (candidates.Count == 0)
        {
            return [];
        }

        var choices = new List<int>();
        foreach (var _ in context.ForceSwitchSlots.Where(slot => slot))
        {
            if (candidates.Count == 0)
            {
                return [];
            }

            var choice = _randomService.RandomElement(candidates);
            choices.Add(choice);
            candidates.Remove(choice);
        }

        return choices;
    }

    private static List<int> GetSwitchCandidates(BattleContext context)
    {
        var candidates = new List<int>();
        for (var index = 0; index < context.SidePokemon.Count; index++)
        {
            var pokemon = context.SidePokemon[index];
            if (!pokemon.IsActive && !pokemon.IsFainted)
            {
                candidates.Add(index + 1);
            }
        }

        return candidates;
    }

    private List<int> BuildMoveChoices(BattleContext context)
    {
        var choices = new List<int>();
        for (var slotIndex = 0; slotIndex < context.ActiveSlots.Count; slotIndex++)
        {
            var slot = context.ActiveSlots[slotIndex];
            if (slot.Moves.Count == 0)
            {
                return [];
            }

            var availableMoves = new List<int>();
            for (var index = 0; index < slot.Moves.Count; index++)
            {
                if (!slot.Moves[index].IsDisabled)
                {
                    availableMoves.Add(index + 1);
                }
            }

            if (availableMoves.Count == 0)
            {
                return [];
            }

            var opponentTypes = GetOpponentTypes();
            var moveChoice = opponentTypes.Count == 0
                ? _randomService.RandomElement(availableMoves)
                : GetBestMoveChoice(slot, availableMoves, opponentTypes);

            choices.Add(moveChoice);
        }

        return choices;
    }

    private int GetBestMoveChoice(
        BattleActiveSlot slot,
        List<int> availableMoves,
        IReadOnlyList<string> opponentTypes)
    {
        var bestMoves = new List<int>();
        var bestMultiplier = double.MinValue;
        foreach (var moveIndex in availableMoves)
        {
            var move = slot.Moves[moveIndex - 1];
            var multiplier = TypeMatchupTable.GetMultiplier(move.Type, opponentTypes);
            if (multiplier > bestMultiplier)
            {
                bestMultiplier = multiplier;
                bestMoves.Clear();
                bestMoves.Add(moveIndex);
                continue;
            }

            if (multiplier.IsApproximatelyEqualTo(bestMultiplier))
            {
                bestMoves.Add(moveIndex);
            }
        }

        return _randomService.RandomElement(bestMoves);
    }

    private static IReadOnlyList<string> GetOpponentTypes()
    {
        return [];
    }
}
