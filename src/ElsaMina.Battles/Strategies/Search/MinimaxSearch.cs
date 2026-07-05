using System.Globalization;
using System.Text;
using ElsaMina.Battles.Strategies.Simulation;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies.Search;

/// <summary>
/// Depth-limited minimax with alpha-beta pruning over simultaneous turns:
/// at each turn we pick the action maximizing our evaluation while the opponent
/// is assumed to answer with the move that minimizes it (pessimistic opponent model,
/// restricted to the moves it has revealed or is likely to carry per usage stats).
/// </summary>
public class MinimaxSearch : IBattleSearchAlgorithm
{
    // One depth unit = one full turn (both sides act)
    private const int MAX_DEPTH = 5;

    // Small bonus per unspent depth level when the opponent is KOed, so faster wins are preferred
    private const double FAST_KO_BONUS = 0.01;

    public SimulationAction FindBestAction(SimulationModel model)
    {
        var rootState = model.CreateInitialState();
        var rootActions = TurnResolver.EnumerateOurActions(model, rootState);
        if (rootActions.Count == 0)
        {
            return null;
        }

        SimulationAction bestAction = null;
        var bestValue = double.MinValue;
        var alpha = double.MinValue;

        // Human-readable breakdown of the decision, emitted once at the end so the lines stay together
        var log = new StringBuilder();
        log.Append(DescribePosition(model, rootState)).Append('\n');
        log.Append(DescribeOpponentMoves(model)).Append('\n');

        foreach (var action in rootActions)
        {
            var value = MinValue(model, rootState, action, MAX_DEPTH, alpha, double.MaxValue);
            log.Append("  ").Append(DescribeAction(model, action))
                .Append(" -> ").Append(value.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
            // Strict comparison keeps the first best action; moves are enumerated
            // before switches, so ties are resolved in favor of attacking
            if (value > bestValue)
            {
                bestValue = value;
                bestAction = action;
            }

            alpha = Math.Max(alpha, bestValue);
        }

        log.Append("  => chosen: ").Append(DescribeAction(model, bestAction))
            .Append(" (value ").Append(bestValue.ToString("F3", CultureInfo.InvariantCulture)).Append(')');
        Log.Debug("[Battle AI] Minimax move selection\n{Breakdown}", log.ToString());

        return bestAction;
    }

    // ── Human-readable logging helpers ────────────────────────────────────────

    private static string DescribePosition(SimulationModel model, SimulationState state)
    {
        var activeIndex = state.ActiveMemberIndex;
        var activeDescription = activeIndex >= 0 && activeIndex < model.Members.Count
            ? $"{model.Members[activeIndex].Species} ({FormatPercent(state.MemberHpRatios[activeIndex])} HP)"
            : "(must switch)";

        return $"Position: our {activeDescription} vs opponent ({FormatPercent(state.OpponentHpRatio)} HP), " +
               $"opponent bench {model.OpponentBenchAliveCount} alive, " +
               $"opponent passive={model.OpponentIsPassive}, can tera={model.CanTerastallize}, " +
               $"trapped={model.ActiveIsTrapped}";
    }

    private static string DescribeOpponentMoves(SimulationModel model)
    {
        if (model.OpponentMoves.Count == 0)
        {
            return "Opponent moves considered: (none)";
        }

        var moves = string.Join(", ", model.OpponentMoves.Select(move =>
            $"{move.Name} (p={move.Probability.ToString("F2", CultureInfo.InvariantCulture)})"));
        return $"Opponent moves considered: {moves}";
    }

    private static string DescribeAction(SimulationModel model, SimulationAction action)
    {
        if (action == null)
        {
            return "(no action)";
        }

        if (action.Kind == SimulationActionKind.Switch)
        {
            return $"switch to {model.Members[action.MemberIndex].Species}";
        }

        var move = model.Members[action.MemberIndex].Moves[action.MoveListIndex];
        var teraSuffix = action.UseTerastallize ? " + Tera" : "";
        return $"move {move.Name}{teraSuffix}";
    }

    private static string FormatPercent(double ratio) =>
        (ratio * 100).ToString("F0", CultureInfo.InvariantCulture) + "%";

    // Opponent node: it answers our chosen action with its worst move for us
    private static double MinValue(SimulationModel model, SimulationState state,
        SimulationAction ourAction, int depth, double alpha, double beta)
    {
        var worstValue = double.MaxValue;
        var opponentMoveCount = Math.Max(1, model.OpponentMoves.Count);

        for (var opponentMoveIndex = 0; opponentMoveIndex < opponentMoveCount; opponentMoveIndex++)
        {
            var nextState = TurnResolver.Resolve(model, state, ourAction, opponentMoveIndex);
            var value = MaxValue(model, nextState, depth - 1, alpha, beta);
            worstValue = Math.Min(worstValue, value);
            beta = Math.Min(beta, worstValue);
            if (worstValue <= alpha)
            {
                break;
            }
        }

        return worstValue;
    }

    // Our node: pick the action maximizing the evaluation
    private static double MaxValue(SimulationModel model, SimulationState state,
        int depth, double alpha, double beta)
    {
        if (depth <= 0 || TurnResolver.IsTerminal(state))
        {
            return Evaluate(model, state, depth);
        }

        var actions = TurnResolver.EnumerateOurActions(model, state);
        if (actions.Count == 0)
        {
            return Evaluate(model, state, depth);
        }

        var bestValue = double.MinValue;
        foreach (var action in actions)
        {
            var value = MinValue(model, state, action, depth, alpha, beta);
            bestValue = Math.Max(bestValue, value);
            alpha = Math.Max(alpha, bestValue);
            if (bestValue >= beta)
            {
                break;
            }
        }

        return bestValue;
    }

    private static double Evaluate(SimulationModel model, SimulationState state, int remainingDepth)
    {
        var value = StateEvaluator.Evaluate(model, state);
        if (state.OpponentHpRatio <= 0)
        {
            value += remainingDepth * FAST_KO_BONUS;
        }

        return value;
    }
}
