using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ElsaMina.Battles;
using ElsaMina.Battles.Strategies.MiniPoke;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPoke;

/// <summary>
/// Replays battles recorded by tools/minipoke/make_fixtures.py through <see cref="BattleMessageParser"/> and
/// compares a model version's C# encoder, action mapping and ONNX model with what Python produced on the same
/// lines. Each version's parity test runs it with its own encoder and fixtures.
/// </summary>
public class MiniPokeParityChecker
{
    private const double FEATURE_TOLERANCE = 1e-5;
    private const double LOGIT_TOLERANCE = 1e-4;
    private const float MASKED_LOGIT_THRESHOLD = -1e8f;
    private const int MAX_REPORTED_MISMATCHES = 30;
    private const string RECHARGE_ORDER = "/choose move 1";
    private const string TEAM_ORDER = "team order";

    private readonly string _fixturesDirectory;
    private readonly IMiniPokeEncoder _encoder;
    private readonly Func<string, int, string> _describeFeature;

    /// <param name="fixturesDirectory">Where the version's fixtures are copied in the test output.</param>
    /// <param name="encoder">The version's encoder.</param>
    /// <param name="describeFeature">Names a position of an array ("mon_feats", 12) for failure reports.</param>
    public MiniPokeParityChecker(string fixturesDirectory, IMiniPokeEncoder encoder,
        Func<string, int, string> describeFeature)
    {
        _fixturesDirectory = fixturesDirectory;
        _encoder = encoder;
        _describeFeature = describeFeature;
    }

    /// <summary>
    /// One test case per recorded battle side.
    /// </summary>
    public static IEnumerable<TestCaseData> FixtureCases(string fixturesDirectory)
    {
        return Directory.EnumerateFiles(fixturesDirectory, "*.json.gz")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .Select(fileName => new TestCaseData(fileName).SetArgDisplayNames(fileName));
    }

    public MiniPokeFixture Load(string fileName)
    {
        using var stream = new GZipStream(File.OpenRead(Path.Join(_fixturesDirectory, fileName)),
            CompressionMode.Decompress);
        return JsonSerializer.Deserialize<MiniPokeFixture>(stream);
    }

    public int CountDecisions()
    {
        return FixtureCases(_fixturesDirectory).Sum(testCase => Load((string)testCase.Arguments[0]).Decisions.Count);
    }

    /// <summary>
    /// Every difference between the six arrays (and the team order) encoder.py and our encoder produced.
    /// </summary>
    public List<string> FindEncodingMismatches(MiniPokeFixture fixture)
    {
        var mismatches = new List<string>();
        foreach (var (index, decision, context) in Replay(fixture))
        {
            var observation = _encoder.Encode(context);
            if (observation == null)
            {
                mismatches.Add($"decision {index} (line {decision.LineCount}): not encoded");
                continue;
            }

            var where = $"decision {index} (line {decision.LineCount}, turn {context.Tracked.Turn})";
            CompareExactly(mismatches, where, "mon_ids", decision.MonIds, observation.MonIds);
            CompareExactly(mismatches, where, "move_ids", decision.MoveIds, observation.MoveIds);
            CompareFloats(mismatches, where, "mon_feats", decision.MonFeatures, observation.MonFeatures);
            CompareFloats(mismatches, where, "move_feats", decision.MoveFeatures, observation.MoveFeatures);
            CompareFloats(mismatches, where, "field", decision.Field, observation.Field);
            CompareExactly(mismatches, where, "mask", decision.Mask, observation.Mask);
            CompareExactly(mismatches, where, TEAM_ORDER, decision.TeamIdents.ToArray(),
                context.Tracked.Team.Select(pokemon => pokemon.Identifier).ToArray());
        }

        return mismatches;
    }

    /// <summary>
    /// Every legal action whose choice differs from the order poke-env sent for it.
    /// </summary>
    public List<string> FindOrderMismatches(MiniPokeFixture fixture)
    {
        var mismatches = new List<string>();
        foreach (var (index, decision, context) in Replay(fixture))
        {
            var observation = _encoder.Encode(context);
            foreach (var (actionKey, pokeEnvOrder) in decision.Orders.Where(pair => pair.Value != null))
            {
                var action = int.Parse(actionKey, CultureInfo.InvariantCulture);
                var order = observation == null ? "not encoded" : ToOrder(action, observation, context);
                if (order != pokeEnvOrder)
                {
                    mismatches.Add($"decision {index} (line {decision.LineCount}) action {action}: " +
                                   $"poke-env sends '{pokeEnvOrder}', ours means '{order}'");
                }
            }
        }

        return mismatches;
    }

    /// <summary>
    /// Every logit or value where our ONNX Runtime and Python's onnxruntime disagree on the recorded observations.
    /// </summary>
    public static List<string> FindModelMismatches(MiniPokeFixture fixture, IMiniPokeModel model)
    {
        var mismatches = new List<string>();
        foreach (var (decision, index) in fixture.Decisions.Select((decision, index) => (decision, index))
                     .Where(pair => pair.decision.Logits != null))
        {
            var (logits, value) = model.Evaluate(ToObservation(decision));
            for (var action = 0; action < logits.Length; action++)
            {
                var bothMasked = logits[action] < MASKED_LOGIT_THRESHOLD &&
                                 decision.Logits[action] < MASKED_LOGIT_THRESHOLD;
                if (!bothMasked && Math.Abs(logits[action] - decision.Logits[action]) > LOGIT_TOLERANCE)
                {
                    mismatches.Add($"decision {index} logit {action}: python {decision.Logits[action]} vs c# {logits[action]}");
                }
            }

            if (Math.Abs(value - decision.Value) > LOGIT_TOLERANCE)
            {
                mismatches.Add($"decision {index} value: python {decision.Value} vs c# {value}");
            }
        }

        return mismatches;
    }

    public static string Report(MiniPokeFixture fixture, List<string> mismatches)
    {
        var report = new StringBuilder($"{mismatches.Count} mismatch(es) in {fixture.BattleTag} ({fixture.PlayerRole})");
        if (fixture.ErrorLines.Count > 0)
        {
            report.Append($"; poke-env raised on lines {string.Join(", ", fixture.ErrorLines)}");
        }

        foreach (var mismatch in mismatches.Take(MAX_REPORTED_MISMATCHES))
        {
            report.Append('\n').Append(mismatch);
        }

        return report.ToString();
    }

    private static IEnumerable<(int Index, MiniPokeFixtureDecision Decision, BattleContext Context)> Replay(
        MiniPokeFixture fixture)
    {
        var parser = new BattleMessageParser(TestBattleDex.Instance);
        var context = new BattleContext(fixture.BattleTag);
        var appliedLines = 0;
        for (var index = 0; index < fixture.Decisions.Count; index++)
        {
            var decision = fixture.Decisions[index];
            for (; appliedLines < decision.LineCount; appliedLines++)
            {
                // Same splitting as Bot.ReadLine, which skips lines without a message type
                var parts = fixture.Lines[appliedLines].Split("|");
                if (parts.Length >= 2)
                {
                    parser.TryApplyMessage(parts, context.RoomId, context, out _);
                }
            }

            yield return (index, decision, context);
        }
    }

    /// <summary>
    /// What our decision for this action means, written like poke-env's order: switches by name, moves by id.
    /// </summary>
    private static string ToOrder(int action, BattleObservation observation, BattleContext context)
    {
        if (action < BattleObservation.SlotCount)
        {
            var sideIndex = observation.TeamSlotToSideIndex[action];
            return sideIndex < 0 ? "unmapped switch" : $"/choose switch {context.SidePokemon[sideIndex].Ident[4..]}";
        }

        var requestIndex = observation.MoveSlotToRequestIndex[(action - BattleObservation.SlotCount) % BattleObservation.MoveCount];
        if (requestIndex < 0)
        {
            return "unmapped move";
        }

        var moveId = context.ActiveSlots[0].Moves[requestIndex].Id;
        if (moveId == "recharge")
        {
            return RECHARGE_ORDER;
        }

        var terastallize = action >= BattleObservation.SlotCount + BattleObservation.MoveCount;
        return terastallize ? $"/choose move {moveId} terastallize" : $"/choose move {moveId}";
    }

    private static BattleObservation ToObservation(MiniPokeFixtureDecision decision)
    {
        var observation = new BattleObservation(decision.MonFeatures.Length / BattleObservation.MonCount,
            decision.MoveFeatures.Length / (BattleObservation.MonCount * BattleObservation.MoveCount),
            decision.Field.Length);
        decision.MonIds.CopyTo(observation.MonIds, 0);
        decision.MoveIds.CopyTo(observation.MoveIds, 0);
        decision.MonFeatures.Select(value => (float)value).ToArray().CopyTo(observation.MonFeatures, 0);
        decision.MoveFeatures.Select(value => (float)value).ToArray().CopyTo(observation.MoveFeatures, 0);
        decision.Field.Select(value => (float)value).ToArray().CopyTo(observation.Field, 0);
        decision.Mask.CopyTo(observation.Mask, 0);
        return observation;
    }

    private void CompareExactly<T>(List<string> mismatches, string where, string array, T[] expected, T[] actual)
    {
        if (expected.Length != actual.Length)
        {
            mismatches.Add($"{where} {array}: python has {expected.Length} values, c# has {actual.Length}");
            return;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
            {
                mismatches.Add($"{where} {array}[{i}] {Describe(array, i)}: python {expected[i]} vs c# {actual[i]}");
            }
        }
    }

    private void CompareFloats(List<string> mismatches, string where, string array, double[] expected, float[] actual)
    {
        if (expected.Length != actual.Length)
        {
            mismatches.Add($"{where} {array}: python has {expected.Length} values, c# has {actual.Length}");
            return;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            if (Math.Abs(expected[i] - actual[i]) > FEATURE_TOLERANCE)
            {
                mismatches.Add($"{where} {array}[{i}] {Describe(array, i)}: python {expected[i]} vs c# {actual[i]}");
            }
        }
    }

    private string Describe(string array, int index)
    {
        return array == TEAM_ORDER ? $"slot {index}" : _describeFeature(array, index);
    }
}
