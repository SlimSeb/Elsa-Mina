using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Logging;
using Lusamine.DamageCalc;
using Lusamine.DamageCalc.Data;

namespace ElsaMina.Battles.Strategies;

/// <summary>
/// Vibe coded slop, left as is for now
/// todo :
/// - rework needed to implement proper minimax
/// - integrate smogon usage stats for proper move selection
///
/// </summary>
public class CalcBasedBattleDecisionService : IBattleDecisionService
{
    private const int MINIMAX_DEPTH = 4;

    private static readonly IGeneration GENERATION = DataIndex.Create(9);

    private readonly IRandomService _randomService;

    public CalcBasedBattleDecisionService(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public bool TryGetDecision(BattleContext context, out BattleDecision decision)
    {
        decision = null;

        if (context.IsBattleOver || context.Wait)
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
            // Single battle: use minimax to weigh moves vs voluntary switches
            if (context.ActiveSlots.Count == 1 &&
                TryFindBestAction(context, context.ActiveSlots[0],
                    out var bestType, out var bestChoice, out var useTera))
            {
                decision = new BattleDecision(bestType, [bestChoice], useTera);
                return true;
            }

            // Doubles fallback: greedy per-slot move selection
            var (choices, useTerastallize) = BuildMoveChoices(context);
            if (choices.Count == 0)
            {
                return false;
            }

            decision = new BattleDecision(BattleDecisionType.Move, choices, useTerastallize);
            return true;
        }

        return false;
    }

    // ── Minimax (single battle) ───────────────────────────────────────────────

    private sealed record SimState
    {
        public double OurHpRatio { get; init; }
        public double OppHpRatio { get; init; }
        public int OurBenchAlive { get; init; }
        public int OppBenchAlive { get; init; }
    }

    private bool TryFindBestAction(BattleContext context, BattleActiveSlot slot,
        out BattleDecisionType type, out int choice, out bool useTera)
    {
        type = BattleDecisionType.Move;
        choice = 0;
        useTera = false;

        var availableMoves = GetAvailableMoveIndices(slot);
        if (availableMoves.Count == 0)
        {
            return false;
        }

        var activePokemon = context.SidePokemon.FirstOrDefault(p => p.IsActive);
        var opponent = context.ActiveOpponent;

        if (opponent == null || activePokemon == null ||
            !TryBuildOurPokemon(activePokemon, out var ourMon) ||
            !TryBuildOpponentPokemon(opponent, out var oppMon))
        {
            Log.Debug("Minimax: falling back to random (no opponent or build failed)");
            choice = _randomService.RandomElement(availableMoves);
            type = BattleDecisionType.Move;
            return true;
        }

        var ourMaxHp = ourMon.MaxHP(false);
        var oppMaxHp = oppMon.MaxHP(false);
        var switchCandidates = slot.Trapped ? [] : GetSwitchCandidates(context);

        var rootState = new SimState
        {
            OurHpRatio = ourMaxHp > 0 ? (double)activePokemon.CurrentHp / ourMaxHp : 1.0,
            OppHpRatio = opponent.HpPercent / 100.0,
            OurBenchAlive = switchCandidates.Count,
            OppBenchAlive = context.OpponentPokemon.Count(p => !p.IsFainted && !p.IsActive)
        };

        var opponentLastMove = opponent.LastUsedMove;
        var activeOurMoveNames = availableMoves.Select(i => slot.Moves[i - 1].Name).ToList();

        double bestScore = double.MinValue;
        var bestChoice = availableMoves[0];
        var bestTera = false;
        var bestType = BattleDecisionType.Move;

        // Evaluate each available move (and its Tera variant)
        foreach (var moveIndex in availableMoves)
        {
            var moveName = slot.Moves[moveIndex - 1].Name;

            var dealtRatio = ComputeDamageRatio(ourMon, oppMon, moveName, oppMaxHp, pessimistic: false);
            var stateAfterMove = rootState with { OppHpRatio = Math.Max(0.0, rootState.OppHpRatio - dealtRatio) };
            var score = EvaluateAfterOurAction(stateAfterMove, ourMon, oppMon,
                ourMaxHp, oppMaxHp, opponentLastMove, activeOurMoveNames, MINIMAX_DEPTH - 1);

            if (score > bestScore)
            {
                bestScore = score;
                bestChoice = moveIndex;
                bestTera = false;
                bestType = BattleDecisionType.Move;
            }

            // Same move with Terastallization
            if (!string.IsNullOrEmpty(slot.CanTerastallize) &&
                TryBuildOurPokemon(activePokemon, out var teraMon))
            {
                teraMon.TeraType = slot.CanTerastallize;
                var teraDealtRatio = ComputeDamageRatio(teraMon, oppMon, moveName, oppMaxHp, pessimistic: false);
                var teraStateAfter = rootState with { OppHpRatio = Math.Max(0.0, rootState.OppHpRatio - teraDealtRatio) };
                var teraScore = EvaluateAfterOurAction(teraStateAfter, teraMon, oppMon,
                    ourMaxHp, oppMaxHp, opponentLastMove, activeOurMoveNames, MINIMAX_DEPTH - 1);

                if (teraScore > bestScore)
                {
                    bestScore = teraScore;
                    bestChoice = moveIndex;
                    bestTera = true;
                    bestType = BattleDecisionType.Move;
                }
            }
        }

        // Evaluate each voluntary switch — we don't attack; opponent gets a free move on the new pokemon
        foreach (var candidateIndex in switchCandidates)
        {
            var candidate = context.SidePokemon[candidateIndex - 1];
            if (!TryBuildOurPokemon(candidate, out var candMon))
            {
                continue;
            }

            var candMaxHp = candMon.MaxHP(false);
            var candHpRatio = candMaxHp > 0 ? (double)candidate.CurrentHp / candMaxHp : 1.0;
            var stateAfterSwitch = rootState with { OurHpRatio = candHpRatio };
            var switchScore = EvaluateAfterOurAction(stateAfterSwitch, candMon, oppMon,
                candMaxHp, oppMaxHp, opponentLastMove, candidate.Moves, MINIMAX_DEPTH - 1);

            if (switchScore > bestScore)
            {
                bestScore = switchScore;
                bestChoice = candidateIndex;
                bestTera = false;
                bestType = BattleDecisionType.Switch;
            }
        }

        choice = bestChoice;
        type = bestType;
        useTera = bestTera;
        return true;
    }

    // Minimize node — opponent responds to our action
    private static double EvaluateAfterOurAction(SimState state, Pokemon ourMon, Pokemon oppMon,
        int ourMaxHp, int oppMaxHp, string opponentLastMove, List<string> ourMoveNames, int depth)
    {
        if (state.OurHpRatio <= 0 || state.OppHpRatio <= 0 || depth == 0)
        {
            return Evaluate(state);
        }

        if (string.IsNullOrEmpty(opponentLastMove))
        {
            return Evaluate(state);
        }

        var damageToUs = ComputeDamageRatio(oppMon, ourMon, opponentLastMove, ourMaxHp, pessimistic: true);
        var stateAfterOpp = state with { OurHpRatio = Math.Max(0.0, state.OurHpRatio - damageToUs) };

        return EvaluateAfterOpponentAction(stateAfterOpp, ourMon, oppMon,
            ourMaxHp, oppMaxHp, opponentLastMove, ourMoveNames, depth - 1);
    }

    // Maximize node — we respond to the opponent's action
    private static double EvaluateAfterOpponentAction(SimState state, Pokemon ourMon, Pokemon oppMon,
        int ourMaxHp, int oppMaxHp, string opponentLastMove, List<string> ourMoveNames, int depth)
    {
        if (state.OurHpRatio <= 0 || state.OppHpRatio <= 0 || depth == 0)
        {
            return Evaluate(state);
        }

        var bestScore = double.MinValue;
        foreach (var moveName in ourMoveNames)
        {
            var dealtRatio = ComputeDamageRatio(ourMon, oppMon, moveName, oppMaxHp, pessimistic: false);
            var newState = state with { OppHpRatio = Math.Max(0.0, state.OppHpRatio - dealtRatio) };
            var score = EvaluateAfterOurAction(newState, ourMon, oppMon,
                ourMaxHp, oppMaxHp, opponentLastMove, ourMoveNames, depth - 1);
            if (score > bestScore)
            {
                bestScore = score;
            }
        }

        return bestScore == double.MinValue ? Evaluate(state) : bestScore;
    }

    private static double ComputeDamageRatio(Pokemon attacker, Pokemon defender,
        string moveName, int defenderMaxHp, bool pessimistic)
    {
        if (defenderMaxHp <= 0)
        {
            return 0.0;
        }

        try
        {
            var calcMove = new Move(GENERATION, moveName);
            var result = Calc.Calculate(GENERATION, attacker, defender, calcMove, null);
            var (minDmg, maxDmg) = result.Range();
            var damage = pessimistic ? maxDmg : (minDmg + maxDmg) / 2.0;
            return damage / defenderMaxHp;
        }
        catch
        {
            return 0.0;
        }
    }

    private static double Evaluate(SimState state)
    {
        var koBonus = 0.0;
        if (state.OppHpRatio <= 0) koBonus += 2.0;
        if (state.OurHpRatio <= 0) koBonus -= 2.0;
        var hpScore = state.OurHpRatio - state.OppHpRatio;
        var benchScore = 0.05 * (state.OurBenchAlive - state.OppBenchAlive);
        return hpScore + koBonus + benchScore;
    }

    // ── Doubles fallback: greedy move selection ───────────────────────────────

    private (List<int> choices, bool useTerastallize) BuildMoveChoices(BattleContext context)
    {
        var choices = new List<int>();
        var useTerastallize = false;

        for (var slotIndex = 0; slotIndex < context.ActiveSlots.Count; slotIndex++)
        {
            var slot = context.ActiveSlots[slotIndex];
            if (slot.Moves.Count == 0)
            {
                Log.Debug("No moves for slot {SlotIndex}", slotIndex);
                return ([], false);
            }

            var availableMoves = GetAvailableMoveIndices(slot);
            if (availableMoves.Count == 0)
            {
                Log.Debug("No available moves for slot {SlotIndex}", slotIndex);
                return ([], false);
            }

            var opponent = context.ActiveOpponent;
            var activePokemon = context.SidePokemon.FirstOrDefault(p => p.IsActive);

            if (opponent == null || activePokemon == null ||
                !TryBuildOurPokemon(activePokemon, out var calcAttacker) ||
                !TryBuildOpponentPokemon(opponent, out var calcDefender))
            {
                Log.Debug("Failed to build calc Pokemon for slot {SlotIndex} => random", slotIndex);
                choices.Add(_randomService.RandomElement(availableMoves));
                continue;
            }

            var (bestMoveIndex, bestScore) = ScoreMoves(slot, availableMoves, calcAttacker, calcDefender);

            // Check if Terastallizing improves the outcome
            if (!string.IsNullOrEmpty(slot.CanTerastallize) &&
                TryBuildOurPokemon(activePokemon, out var teraAttacker))
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
                var calcMove = new Move(GENERATION, move.Name);
                var result = Calc.Calculate(GENERATION, attacker, defender, calcMove, null);
                var score = ComputeMoveScore(result, defender);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = moveIndex;
                }
            }
            catch
            {
                // Move not in dex or produces no damage (status move) — skip
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

    // ── Forced switch selection ───────────────────────────────────────────────

    private List<int> BuildSwitchChoices(BattleContext context)
    {
        var candidates = GetSwitchCandidates(context);
        if (candidates.Count == 0)
        {
            return [];
        }

        var choices = new List<int>();
        var remainingCandidates = new List<int>(candidates);

        foreach (var _ in context.ForceSwitchSlots.Where(slot => slot))
        {
            if (remainingCandidates.Count == 0)
            {
                return [];
            }

            var choice = PickBestSwitchIn(context, remainingCandidates);
            choices.Add(choice);
            remainingCandidates.Remove(choice);
        }

        return choices;
    }

    private int PickBestSwitchIn(BattleContext context, List<int> candidateIndices)
    {
        var opponent = context.ActiveOpponent;

        if (opponent == null || string.IsNullOrEmpty(opponent.LastUsedMove) ||
            !TryBuildOpponentPokemon(opponent, out var calcOpponent))
        {
            return _randomService.RandomElement(candidateIndices);
        }

        var bestIndex = candidateIndices[0];
        var bestDamageTakenPercent = double.MaxValue;

        foreach (var candidateIndex in candidateIndices)
        {
            var candidate = context.SidePokemon[candidateIndex - 1];
            if (!TryBuildOurPokemon(candidate, out var calcCandidate))
            {
                continue;
            }

            try
            {
                var incomingMove = new Move(GENERATION, opponent.LastUsedMove);
                var result = Calc.Calculate(GENERATION, calcOpponent, calcCandidate, incomingMove, null);
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

    // ── Pokemon building helpers ──────────────────────────────────────────────

    private static bool TryBuildOurPokemon(BattlePokemonState state, out Pokemon pokemon)
    {
        pokemon = null;
        var species = ExtractSpeciesFromDetails(state.Details);
        var level = ExtractLevelFromDetails(state.Details);

        try
        {
            pokemon = new Pokemon(GENERATION, species, new State.Pokemon
            {
                Level = level,
                Item = string.IsNullOrEmpty(state.Item) ? null : state.Item,
                Ability = string.IsNullOrEmpty(state.Ability) ? null : state.Ability,
                TeraType = string.IsNullOrEmpty(state.Terastallized) ? null : state.Terastallized,
                Status = ExtractStatus(state.Condition),
                CurHP = state.CurrentHp > 0 ? state.CurrentHp : null
            });

            // Override with the exact in-battle stats from the request JSON
            pokemon.RawStats = new StatsTable
            {
                Hp = state.MaxHp,
                Atk = state.Stats.Atk,
                Def = state.Stats.Def,
                Spa = state.Stats.SpA,
                Spd = state.Stats.SpD,
                Spe = state.Stats.Spe
            };

            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to build calc Pokemon for {Species}", species);
            return false;
        }
    }

    private static bool TryBuildOpponentPokemon(OpponentPokemonState state, out Pokemon pokemon)
    {
        pokemon = null;

        try
        {
            pokemon = new Pokemon(GENERATION, state.Species, new State.Pokemon
            {
                Level = state.Level,
                Status = string.IsNullOrEmpty(state.Status) ? null : state.Status,
                Boosts = BuildBoostsInput(state.Boosts)
            });

            // Apply tracked HP percentage — derive actual HP from computed max
            var maxHp = pokemon.MaxHP(false);
            pokemon.OriginalCurHP = (int)Math.Max(1, Math.Round(maxHp * state.HpPercent / 100.0));

            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to build calc Pokemon for opponent {Species}", state.Species);
            return false;
        }
    }

    // ── Static utility helpers ────────────────────────────────────────────────

    private static StatsTableInput BuildBoostsInput(Dictionary<string, int> boosts)
    {
        return new StatsTableInput
        {
            Atk = GetBoost(boosts, "atk"),
            Def = GetBoost(boosts, "def"),
            Spa = GetBoost(boosts, "spa"),
            Spd = GetBoost(boosts, "spd"),
            Spe = GetBoost(boosts, "spe")
        };
    }

    private static int? GetBoost(Dictionary<string, int> boosts, string key) =>
        boosts.TryGetValue(key, out var value) && value != 0 ? value : null;

    private static string ExtractSpeciesFromDetails(string details)
    {
        var commaIndex = details.IndexOf(',');
        return commaIndex < 0 ? details : details[..commaIndex];
    }

    private static int ExtractLevelFromDetails(string details)
    {
        foreach (var token in details.Split(", ").AsSpan(1))
        {
            if (token.StartsWith('L') && int.TryParse(token.AsSpan(1), out var level))
            {
                return level;
            }
        }

        return 100;
    }

    private static string ExtractStatus(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return null;
        }

        var spaceIndex = condition.IndexOf(' ');
        if (spaceIndex < 0)
        {
            return null;
        }

        var statusPart = condition[(spaceIndex + 1)..];
        return statusPart is "fnt" or "" ? null : statusPart;
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
