using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Battles.Strategies.Simulation;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Logging;
using Lusamine.DamageCalc;

namespace ElsaMina.Battles.Strategies;

/// <summary>
/// Damage-calc based strategy for single battles: builds a simulation model of the current
/// position (our team, the opponent's active pokemon and the moves it has revealed or is
/// likely to carry per Smogon usage stats) and searches it for the best move or switch.
/// Doubles fall back to greedy per-slot move selection.
/// </summary>
public class CalcBasedBattleDecisionService : IBattleDecisionService
{
    private readonly IRandomService _randomService;
    private readonly IOpponentMovesPredictor _opponentMovesPredictor;
    private readonly IBattleSearchAlgorithm _searchAlgorithm;

    public CalcBasedBattleDecisionService(IRandomService randomService,
        IOpponentMovesPredictor opponentMovesPredictor,
        IBattleSearchAlgorithm searchAlgorithm)
    {
        _randomService = randomService;
        _opponentMovesPredictor = opponentMovesPredictor;
        _searchAlgorithm = searchAlgorithm;
    }

    public async Task<BattleDecision> GetDecisionAsync(BattleContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.IsBattleOver || context.Wait)
        {
            return null;
        }

        if (context.TeamPreview && context.SidePokemon.Count > 0)
        {
            var choice = _randomService.NextInt(1, context.SidePokemon.Count + 1);
            return new BattleDecision(BattleDecisionType.TeamPreview, [choice]);
        }

        if (context.ForceSwitchSlots.Any(slot => slot))
        {
            return await GetForcedSwitchDecisionAsync(context, cancellationToken);
        }

        if (context.ActiveSlots.Count == 1)
        {
            return await GetSingleBattleDecisionAsync(context, cancellationToken);
        }

        if (context.ActiveSlots.Count > 1)
        {
            var (choices, useTerastallize) = BuildGreedyMoveChoices(context);
            return choices.Count == 0
                ? null
                : new BattleDecision(BattleDecisionType.Move, choices, useTerastallize);
        }

        return null;
    }

    // ── Single battle: search over the simulation model ──────────────────────

    private async Task<BattleDecision> GetSingleBattleDecisionAsync(BattleContext context,
        CancellationToken cancellationToken)
    {
        var model = await TryBuildModelAsync(context, forcedSwitch: false, cancellationToken);
        var action = model == null ? null : _searchAlgorithm.FindBestAction(model);

        if (action == null)
        {
            Log.Debug("Search produced no action, falling back to a random move");
            return GetRandomMoveDecision(context);
        }

        if (action.Kind == SimulationActionKind.Switch)
        {
            return new BattleDecision(BattleDecisionType.Switch,
                [model.Members[action.MemberIndex].TeamSlot]);
        }

        var move = model.Members[action.MemberIndex].Moves[action.MoveListIndex];
        if (move.RequestMoveIndex <= 0)
        {
            Log.Debug("Search picked a move without request index, falling back to a random move");
            return GetRandomMoveDecision(context);
        }

        return new BattleDecision(BattleDecisionType.Move, [move.RequestMoveIndex], action.UseTerastallize);
    }

    private BattleDecision GetRandomMoveDecision(BattleContext context)
    {
        if (context.ActiveSlots.Count == 0)
        {
            return null;
        }

        var availableMoves = GetAvailableMoveIndices(context.ActiveSlots[0]);
        return availableMoves.Count == 0
            ? null
            : new BattleDecision(BattleDecisionType.Move, [_randomService.RandomElement(availableMoves)]);
    }

    // ── Forced switch ─────────────────────────────────────────────────────────

    private async Task<BattleDecision> GetForcedSwitchDecisionAsync(BattleContext context,
        CancellationToken cancellationToken)
    {
        var switchSlotCount = context.ForceSwitchSlots.Count(slot => slot);

        if (switchSlotCount == 1)
        {
            var model = await TryBuildModelAsync(context, forcedSwitch: true, cancellationToken);
            var action = model == null ? null : _searchAlgorithm.FindBestAction(model);
            if (action is { Kind: SimulationActionKind.Switch })
            {
                return new BattleDecision(BattleDecisionType.Switch,
                    [model.Members[action.MemberIndex].TeamSlot]);
            }
        }

        // Doubles or model failure: pick the candidates taking the least damage from the last seen move
        var candidates = GetSwitchCandidates(context);
        var choices = new List<int>();
        for (var slotIndex = 0; slotIndex < switchSlotCount; slotIndex++)
        {
            if (candidates.Count == 0)
            {
                return null;
            }

            var choice = PickLeastDamagedSwitchIn(context, candidates);
            choices.Add(choice);
            candidates.Remove(choice);
        }

        return choices.Count == 0 ? null : new BattleDecision(BattleDecisionType.Switch, choices);
    }

    private int PickLeastDamagedSwitchIn(BattleContext context, List<int> candidateIndices)
    {
        var opponent = context.ActiveOpponent;

        if (opponent == null || string.IsNullOrEmpty(opponent.LastUsedMove) ||
            !CalcPokemonFactory.TryBuildOpponentPokemon(opponent, out var calcOpponent))
        {
            return _randomService.RandomElement(candidateIndices);
        }

        var bestIndex = candidateIndices[0];
        var bestDamageTakenPercent = double.MaxValue;

        foreach (var candidateIndex in candidateIndices)
        {
            var candidate = context.SidePokemon[candidateIndex - 1];
            if (!CalcPokemonFactory.TryBuildOurPokemon(candidate, out var calcCandidate))
            {
                continue;
            }

            try
            {
                var incomingMove = new Move(CalcPokemonFactory.Generation, opponent.LastUsedMove);
                var result = Calc.Calculate(CalcPokemonFactory.Generation, calcOpponent, calcCandidate,
                    incomingMove, null);
                var (_, maxDamage) = result.Range();
                var damageTakenPercent = (double)maxDamage / calcCandidate.MaxHP(false);

                if (damageTakenPercent < bestDamageTakenPercent)
                {
                    bestDamageTakenPercent = damageTakenPercent;
                    bestIndex = candidateIndex;
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to evaluate switch-in for {Ident}", candidate.Ident);
            }
        }

        return bestIndex;
    }

    // ── Model building ────────────────────────────────────────────────────────

    private async Task<SimulationModel> TryBuildModelAsync(BattleContext context, bool forcedSwitch,
        CancellationToken cancellationToken)
    {
        var opponent = context.ActiveOpponent;
        if (opponent == null)
        {
            return null;
        }

        try
        {
            var prediction = await _opponentMovesPredictor.PredictAsync(context.Format,
                opponent.Species, opponent.RevealedMoves, cancellationToken);
            return SimulationModelBuilder.TryBuild(context, prediction, forcedSwitch);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to build simulation model for {RoomId}", context.RoomId);
            return null;
        }
    }

    // ── Doubles fallback: greedy move selection ───────────────────────────────

    private (List<int> choices, bool useTerastallize) BuildGreedyMoveChoices(BattleContext context)
    {
        var choices = new List<int>();
        var useTerastallize = false;

        for (var slotIndex = 0; slotIndex < context.ActiveSlots.Count; slotIndex++)
        {
            var slot = context.ActiveSlots[slotIndex];
            var availableMoves = GetAvailableMoveIndices(slot);
            if (availableMoves.Count == 0)
            {
                Log.Debug("No available moves for slot {SlotIndex}", slotIndex);
                return ([], false);
            }

            var opponent = context.ActiveOpponent;
            var activePokemon = context.SidePokemon.FirstOrDefault(pokemon => pokemon.IsActive);

            if (opponent == null || activePokemon == null ||
                !CalcPokemonFactory.TryBuildOurPokemon(activePokemon, out var calcAttacker) ||
                !CalcPokemonFactory.TryBuildOpponentPokemon(opponent, out var calcDefender))
            {
                Log.Debug("Failed to build calc Pokemon for slot {SlotIndex} => random", slotIndex);
                choices.Add(_randomService.RandomElement(availableMoves));
                continue;
            }

            var (bestMoveIndex, bestScore) = ScoreMoves(slot, availableMoves, calcAttacker, calcDefender);

            // Check if Terastallizing improves the outcome
            if (!string.IsNullOrEmpty(slot.CanTerastallize) &&
                CalcPokemonFactory.TryBuildOurPokemon(activePokemon, out var teraAttacker))
            {
                teraAttacker.TeraType = slot.CanTerastallize;
                var (teraBestIndex, teraScore) = ScoreMoves(slot, availableMoves, teraAttacker, calcDefender);
                if (teraScore > bestScore)
                {
                    bestMoveIndex = teraBestIndex;
                    useTerastallize = true;
                }
            }

            choices.Add(bestMoveIndex);
        }

        return (choices, useTerastallize);
    }

    private static (int moveIndex, double score) ScoreMoves(
        BattleActiveSlot slot,
        List<int> availableMoveIndices,
        Pokemon attacker,
        Pokemon defender)
    {
        var bestIndex = availableMoveIndices[0];
        var bestScore = double.MinValue;

        foreach (var moveIndex in availableMoveIndices)
        {
            var move = slot.Moves[moveIndex - 1];
            try
            {
                var calcMove = new Move(CalcPokemonFactory.Generation, move.Name);
                var result = Calc.Calculate(CalcPokemonFactory.Generation, attacker, defender, calcMove, null);
                var score = ComputeMoveScore(result, defender);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = moveIndex;
                }
            }
            catch
            {
                // Move not in dex or produces no damage (status move) - skip
            }
        }

        return (bestIndex, bestScore);
    }

    private static double ComputeMoveScore(Result result, Pokemon defender)
    {
        var (koChance, nHko, _) = result.Kochance(false);
        var (_, maxDamage) = result.Range();
        var maxHp = defender.MaxHP(false);

        // Primary: fewest hits to KO (OHKO > 2HKO > ...), secondary: KO chance, tertiary: raw damage %
        var nkoScore = nHko > 0 ? 1000.0 / nHko : 0.0;
        var damagePercent = maxHp > 0 ? (double)maxDamage / maxHp : 0.0;
        return nkoScore + koChance * 100.0 + damagePercent;
    }

    // ── Static utility helpers ────────────────────────────────────────────────

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
}
