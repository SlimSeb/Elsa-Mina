using System.Text.Json.Nodes;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Tracking;

namespace ElsaMina.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// Search at decision time (tools/minipoke/search.py, where it was measured): a one-turn lookahead on Showdown's
/// simulator, scored by the model's value head.
/// <list type="number">
/// <item>Our candidates are the model's most likely allowed actions.</item>
/// <item>The simulator rebuilds the position a few times, each with its own guess of the opponent's hidden sets, and
/// plays one turn for each of our candidates against each of the opponent's actions.</item>
/// <item>Each simulated turn is replayed, after the battle's own lines, into a fresh tracked state, encoded and scored
/// like training scores it: the turn's faint reward plus gamma times the model's value (+-1 when it ended the battle).
/// </item>
/// <item>Per guess, the turn is a zero-sum game: the opponent plays its equilibrium mix, each candidate scores its
/// expected value against it; the scores are averaged over the guesses, and the best candidate is played.</item>
/// </list>
/// </summary>
public class MiniPokeTurnSearch
{
    private const int FIRST_MOVE_ACTION = 6;
    private const int FIRST_TERA_ACTION = 10;
    private const double MIN_PROBABILITY_LOG = 1e-6;

    private readonly IShowdownSimulator _simulator;
    private readonly IBattleMessageParser _parser;
    private readonly MiniPokeSearchOptions _options;

    public MiniPokeTurnSearch(IShowdownSimulator simulator, IBattleMessageParser parser)
        : this(simulator, parser, new MiniPokeSearchOptions())
    {
    }

    public MiniPokeTurnSearch(IShowdownSimulator simulator, IBattleMessageParser parser, MiniPokeSearchOptions options)
    {
        _simulator = simulator;
        _parser = parser;
        _options = options;
    }

    public bool IsAvailable => _simulator.IsAvailable;

    public TimeSpan Timeout => _options.Timeout;

    /// <summary>
    /// The action to play after searching among the <paramref name="allowed"/> ones, or null when there is nothing
    /// to search (a forced switch, a single candidate) or nothing came out of it.
    /// </summary>
    public async Task<int?> ChooseAsync(BattleContext context, float[] logits, bool[] allowed,
        BattleObservation observation, IMiniPokeModel model, IMiniPokeEncoder encoder,
        CancellationToken cancellationToken = default)
    {
        var state = context.Tracked;
        if (!IsAvailable || state.ForceSwitch || state.ActivePokemon == null)
        {
            return null;
        }

        var probabilities = Softmax(logits, allowed);
        var candidates = Candidates(probabilities, context, observation);
        if (candidates.Count < 2)
        {
            return null;
        }

        var request = new JsonObject
        {
            ["position"] = SearchPositionBuilder.Build(context),
            ["ours"] = new JsonArray(candidates.Select(candidate => candidate.Action.DeepClone()).ToArray()),
            ["determinizations"] = _options.Determinizations,
            ["samples"] = _options.Samples,
            ["seed"] = Random.Shared.Next()
        };
        var reply = await _simulator.SimulateAsync(request, cancellationToken);

        var baseLines = context.Lines.Select(line => line.Split('|')).ToList();
        var rootFainted = Fainted(state);
        var perGuess = new List<double[]>();
        foreach (var determinization in reply.Determinizations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payoff = Payoff(determinization, candidates.Count, context, baseLines, rootFainted, model, encoder);
            var scores = ScoresAgainstEquilibrium(payoff);
            if (scores.Length > 0)
            {
                perGuess.Add(scores);
            }
        }

        return Best(candidates, perGuess, probabilities);
    }

    private List<(int Index, JsonObject Action)> Candidates(double[] probabilities, BattleContext context,
        BattleObservation observation)
    {
        var ranked = Enumerable.Range(0, probabilities.Length)
            .Where(action => probabilities[action] > 0)
            .OrderByDescending(action => probabilities[action])
            .Take(_options.TopK)
            .Where((action, rank) => rank == 0 || probabilities[action] >= _options.MinProbability);
        var candidates = new List<(int Index, JsonObject Action)>();
        foreach (var action in ranked)
        {
            if (TrySimulatedAction(action, context, observation, out var simulated))
            {
                candidates.Add((action, simulated));
            }
        }

        return candidates;
    }

    /// <summary>
    /// A compact action as search_sim.js plays it: a switch by name, a move by id.
    /// </summary>
    private static bool TrySimulatedAction(int action, BattleContext context, BattleObservation observation,
        out JsonObject simulated)
    {
        simulated = null;
        if (action < FIRST_MOVE_ACTION)
        {
            var team = context.Tracked.Team;
            if (action < team.Count)
            {
                simulated = new JsonObject
                {
                    ["kind"] = "switch", ["name"] = SearchPositionBuilder.NameOf(team[action].Identifier)
                };
            }

            return simulated != null;
        }

        var move = observation.MoveSlotIds[(action - FIRST_MOVE_ACTION) % BattleObservation.MoveCount];
        if (move == null || PokeEnvNames.SpecialMoves.Contains(move))
        {
            return false;
        }

        simulated = new JsonObject { ["kind"] = "move", ["move"] = move, ["tera"] = action >= FIRST_TERA_ACTION };
        return true;
    }

    private double[,] Payoff(SimulationDeterminization determinization, int candidates, BattleContext context,
        List<string[]> baseLines, (int Ours, int Theirs) rootFainted, IMiniPokeModel model, IMiniPokeEncoder encoder)
    {
        var payoff = new double[candidates, determinization.Theirs.Count];
        for (var a = 0; a < candidates; a++)
        {
            for (var b = 0; b < determinization.Theirs.Count; b++)
            {
                var samples = a < determinization.Leaves.Count && b < determinization.Leaves[a].Count
                    ? determinization.Leaves[a][b]
                    : [];
                var scores = samples
                    .Where(leaf => leaf != null)
                    .Select(leaf => Score(leaf, context, baseLines, rootFainted, model, encoder))
                    .Where(score => score.HasValue)
                    .Select(score => score.Value)
                    .ToList();
                payoff[a, b] = scores.Count > 0 ? scores.Average() : double.NaN;
            }
        }

        return payoff;
    }

    private double? Score(SimulationLeaf leaf, BattleContext context, List<string[]> baseLines,
        (int Ours, int Theirs) rootFainted, IMiniPokeModel model, IMiniPokeEncoder encoder)
    {
        if (leaf.Winner != null)
        {
            if (leaf.Winner == "tie")
            {
                return 0.0;
            }

            return leaf.Winner == context.Tracked.PlayerRole ? 1.0 : -1.0;
        }

        var leafContext = Replay(context.RoomId, baseLines, leaf.Lines);
        var observation = encoder.Encode(leafContext);
        if (observation == null)
        {
            return null;
        }

        var value = model.Evaluate(observation).Value;
        var (ours, theirs) = Fainted(leafContext.Tracked);
        var reward = _options.FaintReward * ((theirs - rootFainted.Theirs) - (ours - rootFainted.Ours));
        return reward + _options.Gamma * value;
    }

    /// <summary>
    /// The battle after the simulated turn: its lines so far, then the turn's, through the same parser as live (the
    /// request stays the last one received, like poke-env's copy in search.py).
    /// </summary>
    private BattleContext Replay(string roomId, List<string[]> baseLines, List<string> simulated)
    {
        var leafContext = new BattleContext(roomId);
        foreach (var parts in baseLines)
        {
            _parser.TryApplyMessage(parts, roomId, leafContext, out _);
        }

        foreach (var line in simulated)
        {
            _parser.TryApplyMessage(line.Split('|'), roomId, leafContext, out _);
        }

        var state = leafContext.Tracked;
        state.ForceSwitch = state.ActivePokemon is { Fainted: true };
        state.Trapped = false;
        if (state.UsedTera)
        {
            state.CanTera = false;
        }

        return leafContext;
    }

    /// <summary>
    /// Each candidate's expected score against the opponent's equilibrium mix, NaN for a candidate without a valid
    /// turn; empty when no game can be played.
    /// </summary>
    private double[] ScoresAgainstEquilibrium(double[,] payoff)
    {
        var rows = Enumerable.Range(0, payoff.GetLength(0))
            .Where(a => Enumerable.Range(0, payoff.GetLength(1)).Any(b => !double.IsNaN(payoff[a, b])))
            .ToList();
        var columns = Enumerable.Range(0, payoff.GetLength(1))
            .Where(b => rows.TrueForAll(a => !double.IsNaN(payoff[a, b])))
            .ToList();
        if (rows.Count == 0 || columns.Count == 0)
        {
            return [];
        }

        var game = new double[rows.Count, columns.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            for (var j = 0; j < columns.Count; j++)
            {
                game[i, j] = payoff[rows[i], columns[j]];
            }
        }

        var (_, theirs) = ZeroSumSolver.Solve(game, _options.Iterations);
        var scores = Enumerable.Repeat(double.NaN, payoff.GetLength(0)).ToArray();
        for (var i = 0; i < rows.Count; i++)
        {
            scores[rows[i]] = Enumerable.Range(0, columns.Count).Sum(j => game[i, j] * theirs[j]);
        }

        return scores;
    }

    private int? Best(List<(int Index, JsonObject Action)> candidates, List<double[]> perGuess,
        double[] probabilities)
    {
        int? best = null;
        var bestScore = double.NegativeInfinity;
        for (var a = 0; a < candidates.Count; a++)
        {
            var guesses = perGuess.Select(scores => scores[a]).Where(score => !double.IsNaN(score)).ToList();
            if (guesses.Count == 0)
            {
                continue;
            }

            var probability = Math.Max(probabilities[candidates[a].Index], MIN_PROBABILITY_LOG);
            var score = guesses.Average() + _options.PriorWeight * Math.Log(probability);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidates[a].Index;
            }
        }

        return best;
    }

    private static (int Ours, int Theirs) Fainted(TrackedBattleState state) =>
        (state.Team.Count(pokemon => pokemon.Fainted), state.OpponentTeam.Count(pokemon => pokemon.Fainted));

    private static double[] Softmax(float[] logits, bool[] allowed)
    {
        var max = logits.Where((_, i) => allowed[i]).DefaultIfEmpty(0f).Max();
        var weights = logits.Select((logit, i) => allowed[i] ? Math.Exp(logit - max) : 0.0).ToArray();
        var total = weights.Sum();
        return weights.Select(weight => total > 0 ? weight / total : 0.0).ToArray();
    }
}
