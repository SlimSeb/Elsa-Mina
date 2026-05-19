using ElsaMina.Core.Services.Probabilities;

namespace ElsaMina.Battles.Strategies;

public class RandomBattleDecisionService : IBattleDecisionService
{
    private readonly IRandomService _randomService;

    public RandomBattleDecisionService(IRandomService randomService)
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

    private List<int> GetSwitchCandidates(BattleContext context)
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
        foreach (var slot in context.ActiveSlots)
        {
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

            choices.Add(_randomService.RandomElement(availableMoves));
        }

        return choices;
    }
}
