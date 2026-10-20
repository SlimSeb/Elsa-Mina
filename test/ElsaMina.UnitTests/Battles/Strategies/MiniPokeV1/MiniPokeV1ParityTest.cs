using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeV1;
using ElsaMina.UnitTests.Battles.Strategies.MiniPoke;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV1;

/// <summary>
/// Checks the C# side of minipoke v1 against real battles recorded with the encoder.py v1 was trained with
/// (tools/minipoke/make_fixtures.py --version 1): our encoder must produce exactly what encoder.py produced,
/// each action must map to the order poke-env sent, and the ONNX model must give the same output in both runtimes.
/// </summary>
[TestFixture]
public class MiniPokeV1ParityTest
{
    private static readonly string FIXTURES_DIRECTORY =
        Path.Join(AppContext.BaseDirectory, "Battles", "Strategies", "MiniPokeV1", "Fixtures");

    private static readonly MiniPokeModelOptions OPTIONS = new(BattleDecisionStrategy.MiniPokeV1,
        Path.Join(AppContext.BaseDirectory, "Models", "MiniPokeV1"));

    private MiniPokeParityChecker _checker;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var featureNames = new MiniPokeV1FeatureNames(EncoderSpec.Load(OPTIONS.EncoderSpecPath));
        _checker = new MiniPokeParityChecker(FIXTURES_DIRECTORY, new MiniPokeV1Encoder(OPTIONS, TestBattleDex.Instance),
            featureNames.Describe);
    }

    private static IEnumerable<TestCaseData> Fixtures() => MiniPokeParityChecker.FixtureCases(FIXTURES_DIRECTORY);

    [Test]
    public void Test_Fixtures_ShouldCoverEnoughDecisions()
    {
        // Act
        var decisions = _checker.CountDecisions();

        // Assert
        Assert.That(decisions, Is.GreaterThanOrEqualTo(300));
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Test_Encode_ShouldMatchPythonEncoder_WhenReplayingFixture(string fileName)
    {
        // Arrange
        var fixture = _checker.Load(fileName);

        // Act
        var mismatches = _checker.FindEncodingMismatches(fixture);

        // Assert
        Assert.That(mismatches, Is.Empty, MiniPokeParityChecker.Report(fixture, mismatches));
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Test_ActionMapping_ShouldSendPokeEnvOrder_WhenReplayingFixture(string fileName)
    {
        // Arrange
        var fixture = _checker.Load(fileName);

        // Act
        var mismatches = _checker.FindOrderMismatches(fixture);

        // Assert
        Assert.That(mismatches, Is.Empty, MiniPokeParityChecker.Report(fixture, mismatches));
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Test_Evaluate_ShouldMatchPythonOnnxRuntime_WhenGivenFixtureObservations(string fileName)
    {
        // Arrange
        var fixture = _checker.Load(fileName);
        using var model = new MiniPokeOnnxModel(OPTIONS);

        // Act
        var mismatches = MiniPokeParityChecker.FindModelMismatches(fixture, model);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.IsLoaded, Is.True);
            Assert.That(mismatches, Is.Empty, MiniPokeParityChecker.Report(fixture, mismatches));
        }
    }
}
