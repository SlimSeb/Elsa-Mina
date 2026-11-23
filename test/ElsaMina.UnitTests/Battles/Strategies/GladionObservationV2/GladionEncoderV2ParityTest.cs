using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Gladion;
using ElsaMina.Battles.Strategies.GladionObservationV2;
using ElsaMina.UnitTests.Battles.Strategies.Gladion;

namespace ElsaMina.UnitTests.Battles.Strategies.GladionObservationV2;

/// <summary>
/// Checks the C# side of every encoder v2 model against real battles recorded with the encoder_v2.py it was trained
/// with (tools/gladion/make_fixtures.py --version 1_22M for GladionV1_22M): our encoder must produce exactly what
/// encoder_v2.py produced, V1's arrays and the set priors and history ones, each action must map to the order
/// poke-env sent, and the ONNX model must give the same output in both runtimes.
/// </summary>
[TestFixture]
public class GladionEncoderV2ParityTest
{
    // Every model trained with encoder_v2.py, each with its own fixtures
    private static readonly BattleDecisionStrategy[] MODELS =
        [BattleDecisionStrategy.GladionV1_22M];

    private readonly Dictionary<BattleDecisionStrategy, GladionParityChecker> _checkers = [];

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        foreach (var model in MODELS)
        {
            var options = GetOptions(model);
            var spec = EncoderSpec.Load(options.EncoderSpecPath);
            _checkers[model] = new GladionParityChecker(GetFixturesDirectory(model),
                new GladionEncoderV2(options, TestBattleDex.Instance), spec, new GladionEncoderV2FeatureNames(spec).Describe);
        }
    }

    private static GladionModelOptions GetOptions(BattleDecisionStrategy model) =>
        new(model, Path.Join(AppContext.BaseDirectory, "Models", model.ToString()));

    private static string GetFixturesDirectory(BattleDecisionStrategy model) =>
        Path.Join(AppContext.BaseDirectory, "Battles", "Strategies", model.ToString(), "Fixtures");

    private static IEnumerable<TestCaseData> Fixtures() =>
        MODELS.SelectMany(model => GladionParityChecker.FixtureFileNames(GetFixturesDirectory(model))
            .Select(fileName => new TestCaseData(model, fileName).SetArgDisplayNames(model.ToString(), fileName)));

    [TestCaseSource(nameof(MODELS))]
    public void Test_Fixtures_ShouldCoverEnoughDecisions(BattleDecisionStrategy model)
    {
        // Act
        var decisions = _checkers[model].CountDecisions();

        // Assert
        Assert.That(decisions, Is.GreaterThanOrEqualTo(300));
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Test_Encode_ShouldMatchPythonEncoder_WhenReplayingFixture(BattleDecisionStrategy model,
        string fileName)
    {
        // Arrange
        var checker = _checkers[model];
        var fixture = checker.Load(fileName);

        // Act
        var mismatches = checker.FindEncodingMismatches(fixture);

        // Assert
        Assert.That(mismatches, Is.Empty, GladionParityChecker.Report(fixture, mismatches));
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Test_ActionMapping_ShouldSendPokeEnvOrder_WhenReplayingFixture(BattleDecisionStrategy model,
        string fileName)
    {
        // Arrange
        var checker = _checkers[model];
        var fixture = checker.Load(fileName);

        // Act
        var mismatches = checker.FindOrderMismatches(fixture);

        // Assert
        Assert.That(mismatches, Is.Empty, GladionParityChecker.Report(fixture, mismatches));
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Test_Evaluate_ShouldMatchPythonOnnxRuntime_WhenGivenFixtureObservations(BattleDecisionStrategy model,
        string fileName)
    {
        // Arrange
        var checker = _checkers[model];
        var fixture = checker.Load(fileName);
        using var onnxModel = new GladionOnnxModel(GetOptions(model));

        // Act
        var mismatches = checker.FindModelMismatches(fixture, onnxModel);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(onnxModel.IsLoaded, Is.True);
            Assert.That(mismatches, Is.Empty, GladionParityChecker.Report(fixture, mismatches));
        }
    }
}
