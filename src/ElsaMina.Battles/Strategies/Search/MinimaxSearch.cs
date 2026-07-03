using ElsaMina.Battles.Strategies.Simulation;

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
    private const int MAX_DEPTH = 3;

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

        foreach (var action in rootActions)
        {
            var value = MinValue(model, rootState, action, MAX_DEPTH, alpha, double.MaxValue);
            // Strict comparison keeps the first best action; moves are enumerated
            // before switches, so ties are resolved in favor of attacking
            if (value > bestValue)
            {
                bestValue = value;
                bestAction = action;
            }

            alpha = Math.Max(alpha, bestValue);
        }

        return bestAction;
    }

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
