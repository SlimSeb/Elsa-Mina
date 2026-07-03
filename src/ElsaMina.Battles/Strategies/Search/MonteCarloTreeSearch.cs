using ElsaMina.Battles.Strategies.Simulation;
using ElsaMina.Core.Services.Probabilities;

namespace ElsaMina.Battles.Strategies.Search;

/// <summary>
/// Monte Carlo tree search using decoupled UCT, which handles the simultaneous-move
/// structure of a Pokemon turn: at every node each side selects its action with its own
/// UCB statistics (we maximize the evaluation, the opponent minimizes it), the joint
/// action is resolved, and random playouts evaluate new positions.
/// </summary>
public class MonteCarloTreeSearch : IBattleSearchAlgorithm
{
    private const int ITERATIONS = 3000;
    private const int MAX_TREE_DEPTH = 6;
    private const int ROLLOUT_DEPTH = 3;
    private const double EXPLORATION_CONSTANT = 1.5;

    private readonly IRandomService _randomService;

    public MonteCarloTreeSearch(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public SimulationAction FindBestAction(SimulationModel model)
    {
        var root = new SearchNode(model, model.CreateInitialState());
        if (root.OurActions.Count == 0)
        {
            return null;
        }

        if (root.OurActions.Count == 1)
        {
            return root.OurActions[0];
        }

        for (var iteration = 0; iteration < ITERATIONS; iteration++)
        {
            Simulate(model, root, MAX_TREE_DEPTH);
        }

        var mostVisitedIndex = 0;
        for (var actionIndex = 1; actionIndex < root.OurActions.Count; actionIndex++)
        {
            if (root.OurActionVisits[actionIndex] > root.OurActionVisits[mostVisitedIndex])
            {
                mostVisitedIndex = actionIndex;
            }
        }

        return root.OurActions[mostVisitedIndex];
    }

    private double Simulate(SimulationModel model, SearchNode node, int remainingDepth)
    {
        if (node.OurActions.Count == 0 || remainingDepth <= 0 || TurnResolver.IsTerminal(node.State))
        {
            return StateEvaluator.Evaluate(model, node.State);
        }

        var ourActionIndex = SelectOurAction(node);
        var opponentMoveIndex = SelectOpponentMove(node);

        double value;
        if (node.Children.TryGetValue((ourActionIndex, opponentMoveIndex), out var child))
        {
            value = Simulate(model, child, remainingDepth - 1);
        }
        else
        {
            var childState = TurnResolver.Resolve(model, node.State,
                node.OurActions[ourActionIndex], opponentMoveIndex);
            child = new SearchNode(model, childState);
            node.Children[(ourActionIndex, opponentMoveIndex)] = child;
            value = Rollout(model, childState);
        }

        node.VisitCount++;
        node.OurActionVisits[ourActionIndex]++;
        node.OurActionValues[ourActionIndex] += value;
        node.OpponentMoveVisits[opponentMoveIndex]++;
        node.OpponentMoveValues[opponentMoveIndex] += value;
        return value;
    }

    private double Rollout(SimulationModel model, SimulationState state)
    {
        for (var step = 0; step < ROLLOUT_DEPTH && !TurnResolver.IsTerminal(state); step++)
        {
            var actions = TurnResolver.EnumerateOurActions(model, state);
            if (actions.Count == 0)
            {
                break;
            }

            var ourAction = _randomService.RandomElement(actions);
            var opponentMoveIndex = model.OpponentMoves.Count > 0
                ? _randomService.NextInt(model.OpponentMoves.Count)
                : 0;
            state = TurnResolver.Resolve(model, state, ourAction, opponentMoveIndex);
        }

        return StateEvaluator.Evaluate(model, state);
    }

    private static int SelectOurAction(SearchNode node)
    {
        var bestIndex = 0;
        var bestScore = double.MinValue;
        for (var actionIndex = 0; actionIndex < node.OurActions.Count; actionIndex++)
        {
            var visits = node.OurActionVisits[actionIndex];
            if (visits == 0)
            {
                return actionIndex;
            }

            var exploitation = node.OurActionValues[actionIndex] / visits;
            var exploration = EXPLORATION_CONSTANT * Math.Sqrt(Math.Log(node.VisitCount + 1) / visits);
            var score = exploitation + exploration;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = actionIndex;
            }
        }

        return bestIndex;
    }

    private static int SelectOpponentMove(SearchNode node)
    {
        var bestIndex = 0;
        var bestScore = double.MinValue;
        for (var moveIndex = 0; moveIndex < node.OpponentMoveCount; moveIndex++)
        {
            var visits = node.OpponentMoveVisits[moveIndex];
            if (visits == 0)
            {
                return moveIndex;
            }

            // The opponent minimizes our evaluation, hence the negated exploitation term
            var exploitation = -node.OpponentMoveValues[moveIndex] / visits;
            var exploration = EXPLORATION_CONSTANT * Math.Sqrt(Math.Log(node.VisitCount + 1) / visits);
            var score = exploitation + exploration;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = moveIndex;
            }
        }

        return bestIndex;
    }

    private sealed class SearchNode
    {
        public SearchNode(SimulationModel model, SimulationState state)
        {
            State = state;
            OurActions = TurnResolver.EnumerateOurActions(model, state);
            OpponentMoveCount = Math.Max(1, model.OpponentMoves.Count);
            OurActionVisits = new int[OurActions.Count];
            OurActionValues = new double[OurActions.Count];
            OpponentMoveVisits = new int[OpponentMoveCount];
            OpponentMoveValues = new double[OpponentMoveCount];
        }

        public SimulationState State { get; }
        public List<SimulationAction> OurActions { get; }
        public int OpponentMoveCount { get; }
        public int VisitCount { get; set; }
        public int[] OurActionVisits { get; }
        public double[] OurActionValues { get; }
        public int[] OpponentMoveVisits { get; }
        public double[] OpponentMoveValues { get; }
        public Dictionary<(int OurActionIndex, int OpponentMoveIndex), SearchNode> Children { get; } = new();
    }
}
