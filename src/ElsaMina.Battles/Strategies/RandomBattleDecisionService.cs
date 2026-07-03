using ElsaMina.Core.Services.Probabilities;

namespace ElsaMina.Battles.Strategies;

public class RandomBattleDecisionService : IBattleDecisionService
{
    private readonly IRandomService _randomService;

    public RandomBattleDecisionService(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public Task<BattleDecision> GetDecisionAsync(BattleContext context, CancellationToken cancellationToken = default)
    {
        if (context.IsBattleOver)
        {
            return Task.FromResult<BattleDecision>(null);
        }

        if (context.TeamPreview && context.SidePokemon.Count > 0)
        {
            var choice = _randomService.NextInt(1, context.SidePokemon.Count + 1);
            return Task.FromResult(new BattleDecision(BattleDecisionType.TeamPreview, [choice]));
        }

        if (context.ForceSwitchSlots.Any(slot => slot))
        {
            var choices = BuildSwitchChoices(context);
            return Task.FromResult(choices.Count == 0
                ? null
                : new BattleDecision(BattleDecisionType.Switch, choices));
        }

        if (context.ActiveSlots.Count > 0)
        {
            var choices = BuildMoveChoices(context);
            return Task.FromResult(choices.Count == 0
                ? null
                : new BattleDecision(BattleDecisionType.Move, choices));
        }

        return Task.FromResult<BattleDecision>(null);
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
        foreach (var moves in context.ActiveSlots.Select(slot => slot.Moves))
        {
            if (moves.Count == 0)
            {
                return [];
            }

            var availableMoves = Enumerable.Range(0, moves.Count)
                .Where(index => !moves[index].IsDisabled)
                .Select(index => index + 1)
                .ToList();

            if (availableMoves.Count == 0)
            {
                return [];
            }

            choices.Add(_randomService.RandomElement(availableMoves));
        }

        return choices;
    }
}
