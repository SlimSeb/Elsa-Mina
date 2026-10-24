using ElsaMina.Battles;
using ElsaMina.Battles.Results;
using ElsaMina.Battles.Strategies;
using ElsaMina.Core.Services.Clock;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Results;

public class BattleResultsServiceTest
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private IBattleDecisionManager _battleDecisionManager;
    private BattleResultsService _service;

    [SetUp]
    public void SetUp()
    {
        _battleDecisionManager = Substitute.For<IBattleDecisionManager>();
        _battleDecisionManager.ActiveStrategy.Returns(BattleDecisionStrategy.MiniPokeV2_1M);
        var clockService = Substitute.For<IClockService>();
        clockService.CurrentUtcDateTimeOffset.Returns(NOW);
        _service = new BattleResultsService(_battleDecisionManager, clockService);
    }

    private void Play(string roomId, BattleOutcome outcome, int turns = 20)
    {
        var context = new BattleContext(roomId);
        context.Tracked.Turn = turns;
        _service.OnBattleStarted(context);
        _service.OnBattleEnded(context, outcome);
    }

    [Test]
    public void Test_GetRecords_ShouldCountResultsByFormatAndStrategy_MostPlayedFirst()
    {
        // Arrange
        Play("battle-gen9randombattle-1", BattleOutcome.Won);
        Play("battle-gen9ou-2", BattleOutcome.Lost);
        Play("battle-gen9randombattle-3", BattleOutcome.Lost);
        Play("battle-gen9randombattle-4", BattleOutcome.Tied);
        _battleDecisionManager.ActiveStrategy.Returns(BattleDecisionStrategy.MiniPokeV1_4M);
        Play("battle-gen9randombattle-5", BattleOutcome.Won);

        // Act
        var records = _service.GetRecords();

        // Assert
        Assert.That(records, Is.EqualTo(new[]
        {
            new BattleRecord("gen9randombattle", BattleDecisionStrategy.MiniPokeV2_1M, 1, 1, 1),
            new BattleRecord("gen9ou", BattleDecisionStrategy.MiniPokeV2_1M, 0, 1, 0),
            new BattleRecord("gen9randombattle", BattleDecisionStrategy.MiniPokeV1_4M, 1, 0, 0)
        }));
    }

    [Test]
    public void Test_OnBattleEnded_ShouldCountStrategyThatStartedBattle_WhenStrategyChangedSince()
    {
        // Arrange
        var context = new BattleContext("battle-gen9randombattle-1");
        _service.OnBattleStarted(context);
        _battleDecisionManager.ActiveStrategy.Returns(BattleDecisionStrategy.MiniPokeV1_4M);

        // Act
        _service.OnBattleEnded(context, BattleOutcome.Won);

        // Assert
        Assert.That(_service.GetRecords().Single().Strategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV2_1M));
    }

    [Test]
    public void Test_LastResult_ShouldBeLastBattleThatEnded()
    {
        // Arrange
        Play("battle-gen9randombattle-1", BattleOutcome.Won);

        // Act
        Play("battle-gen9randombattle-2", BattleOutcome.Lost, 31);

        // Assert
        Assert.That(_service.LastResult, Is.EqualTo(new BattleResult("battle-gen9randombattle-2", "gen9randombattle",
            BattleDecisionStrategy.MiniPokeV2_1M, BattleOutcome.Lost, 31, NOW)));
    }

    [Test]
    public void Test_LastResult_ShouldBeNull_WhenNoBattleEnded()
    {
        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_service.LastResult, Is.Null);
            Assert.That(_service.GetRecords(), Is.Empty);
        }
    }
}
