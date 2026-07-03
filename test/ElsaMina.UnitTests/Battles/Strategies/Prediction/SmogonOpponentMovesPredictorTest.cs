using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Smogon;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Battles.Strategies.Prediction;

public class SmogonOpponentMovesPredictorTest
{
    private ISmogonUsageDataProvider _smogonUsageDataProvider;
    private IClockService _clockService;

    private SmogonOpponentMovesPredictor _predictor;

    [SetUp]
    public void SetUp()
    {
        _smogonUsageDataProvider = Substitute.For<ISmogonUsageDataProvider>();
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTime.Returns(new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc));

        _predictor = new SmogonOpponentMovesPredictor(_smogonUsageDataProvider, _clockService);
    }

    private void SetUpUsageData()
    {
        var usageData = new SmogonUsageDataDto
        {
            Data = new Dictionary<string, SmogonPokemonUsageDataDto>
            {
                ["Garchomp"] = new()
                {
                    Abilities = new Dictionary<string, double> { ["Rough Skin"] = 100.0 },
                    Moves = new Dictionary<string, double>
                    {
                        ["Earthquake"] = 90.0,
                        ["Stealth Rock"] = 50.0,
                        ["Fire Fang"] = 5.0
                    }
                }
            }
        };
        _smogonUsageDataProvider
            .GetUsageDataAsync("2026-06", "gen9ou", 1760, Arg.Any<CancellationToken>())
            .Returns(usageData);
    }

    [Test]
    public async Task Test_PredictMovesAsync_ShouldMergeRevealedMovesWithUsageMoves()
    {
        // Arrange
        SetUpUsageData();

        // Act
        var predictions = await _predictor.PredictMovesAsync("gen9ou", "Garchomp", ["Dragon Claw"]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(predictions.Single(move => move.Name == "Dragon Claw").Probability, Is.EqualTo(1.0));
            Assert.That(predictions.Single(move => move.Name == "Earthquake").Probability,
                Is.EqualTo(0.9).Within(1e-9));
            Assert.That(predictions.Single(move => move.Name == "Stealth Rock").Probability,
                Is.EqualTo(0.5).Within(1e-9));
            Assert.That(predictions.Any(move => move.Name == "Fire Fang"), Is.False,
                "Moves below the carry probability threshold should be excluded");
        }
    }

    [Test]
    public async Task Test_PredictMovesAsync_ShouldNotDuplicateRevealedMoves_WhenAlsoInUsageData()
    {
        // Arrange
        SetUpUsageData();

        // Act
        var predictions = await _predictor.PredictMovesAsync("gen9ou", "Garchomp", ["Earthquake"]);

        // Assert
        Assert.That(predictions.Count(move => move.Name == "Earthquake"), Is.EqualTo(1));
        Assert.That(predictions.Single(move => move.Name == "Earthquake").Probability, Is.EqualTo(1.0));
    }

    [Test]
    public async Task Test_PredictMovesAsync_ShouldSkipUsageStats_WhenFormatIsRandomBattle()
    {
        // Act
        var predictions = await _predictor.PredictMovesAsync("gen9randombattle", "Garchomp", ["Earthquake"]);

        // Assert
        Assert.That(predictions, Has.Count.EqualTo(1));
        await _smogonUsageDataProvider.DidNotReceiveWithAnyArgs()
            .GetUsageDataAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_PredictMovesAsync_ShouldSkipUsageStats_WhenFourMovesAreAlreadyRevealed()
    {
        // Act
        var predictions = await _predictor.PredictMovesAsync("gen9ou", "Garchomp",
            ["Earthquake", "Dragon Claw", "Stealth Rock", "Fire Fang"]);

        // Assert
        Assert.That(predictions, Has.Count.EqualTo(4));
        await _smogonUsageDataProvider.DidNotReceiveWithAnyArgs()
            .GetUsageDataAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_PredictMovesAsync_ShouldReturnRevealedMovesOnly_WhenUsageDataIsUnavailable()
    {
        // Arrange
        _smogonUsageDataProvider
            .GetUsageDataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("not found"));

        // Act
        var predictions = await _predictor.PredictMovesAsync("gen9uber", "Koraidon", ["Collision Course"]);

        // Assert
        Assert.That(predictions, Has.Count.EqualTo(1));
        Assert.That(predictions[0].Name, Is.EqualTo("Collision Course"));
    }

    [Test]
    public async Task Test_PredictMovesAsync_ShouldCacheUsageData_WhenCalledTwiceForTheSameFormat()
    {
        // Arrange
        SetUpUsageData();

        // Act
        await _predictor.PredictMovesAsync("gen9ou", "Garchomp", []);
        await _predictor.PredictMovesAsync("gen9ou", "Garchomp", ["Earthquake"]);

        // Assert
        await _smogonUsageDataProvider.Received(1)
            .GetUsageDataAsync("2026-06", "gen9ou", 1760, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_PredictMovesAsync_ShouldFallBackToOtherRatingCutoffs_WhenPreferredOneIsMissing()
    {
        // Arrange
        var usageData = new SmogonUsageDataDto
        {
            Data = new Dictionary<string, SmogonPokemonUsageDataDto>
            {
                ["Garchomp"] = new()
                {
                    Abilities = new Dictionary<string, double> { ["Rough Skin"] = 10.0 },
                    Moves = new Dictionary<string, double> { ["Earthquake"] = 9.0 }
                }
            }
        };
        _smogonUsageDataProvider
            .GetUsageDataAsync("2026-06", "gen9lc", 1760, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("not found"));
        _smogonUsageDataProvider
            .GetUsageDataAsync("2026-06", "gen9lc", 1825, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("not found"));
        _smogonUsageDataProvider
            .GetUsageDataAsync("2026-06", "gen9lc", 1500, Arg.Any<CancellationToken>())
            .Returns(usageData);

        // Act
        var predictions = await _predictor.PredictMovesAsync("gen9lc", "Garchomp", []);

        // Assert
        Assert.That(predictions.Single(move => move.Name == "Earthquake").Probability,
            Is.EqualTo(0.9).Within(1e-9));
    }
}
