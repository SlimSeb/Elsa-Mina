using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeV1;
using ElsaMina.UnitTests.Battles.Strategies.MiniPoke;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV1;

/// <summary>
/// Checks the C# side of every minipoke v1 model against real battles recorded with the encoder.py v1 was trained
/// with (tools/minipoke/make_fixtures.py --version 1_4M for MiniPokeV1_4M): our encoder must produce exactly what
/// encoder.py produced, each action must map to the order poke-env sent, and the ONNX model must give the same
/// output in both runtimes.
/// </summary>
[TestFixture]
public class MiniPokeV1ParityTest
{
    // Every model trained with encoder.py v1, each with its own fixtures
    private static readonly BattleDecisionStrategy[] MODELS =
        [BattleDecisionStrategy.MiniPokeV1_1M, BattleDecisionStrategy.MiniPokeV1_4M];

    private readonly Dictionary<BattleDecisionStrategy, MiniPokeParityChecker> _checkers = [];

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        foreach (var model in MODELS)
        {
            var options = GetOptions(model);
            var featureNames = new MiniPokeV1FeatureNames(EncoderSpec.Load(options.EncoderSpecPath));
            _checkers[model] = new MiniPokeParityChecker(GetFixturesDirectory(model),
                new MiniPokeV1Encoder(options, TestBattleDex.Instance), featureNames.Describe);
        }
    }

    private static MiniPokeModelOptions GetOptions(BattleDecisionStrategy model) =>
        new(model, Path.Join(AppContext.BaseDirectory, "Models", model.ToString()));

    private static string GetFixturesDirectory(BattleDecisionStrategy model) =>
        Path.Join(AppContext.BaseDirectory, "Battles", "Strategies", model.ToString(), "Fixtures");

    private static IEnumerable<TestCaseData> Fixtures() =>
        MODELS.SelectMany(model => MiniPokeParityChecker.FixtureFileNames(GetFixturesDirectory(model))
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
        Assert.That(mismatches, Is.Empty, MiniPokeParityChecker.Report(fixture, mismatches));
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
        Assert.That(mismatches, Is.Empty, MiniPokeParityChecker.Report(fixture, mismatches));
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Test_Evaluate_ShouldMatchPythonOnnxRuntime_WhenGivenFixtureObservations(BattleDecisionStrategy model,
        string fileName)
    {
        // Arrange
        var fixture = _checkers[model].Load(fileName);
        using var onnxModel = new MiniPokeOnnxModel(GetOptions(model));

        // Act
        var mismatches = MiniPokeParityChecker.FindModelMismatches(fixture, onnxModel);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(onnxModel.IsLoaded, Is.True);
            Assert.That(mismatches, Is.Empty, MiniPokeParityChecker.Report(fixture, mismatches));
        }
    }
}
