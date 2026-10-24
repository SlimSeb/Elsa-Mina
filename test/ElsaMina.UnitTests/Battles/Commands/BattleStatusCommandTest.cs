using ElsaMina.Battles;
using ElsaMina.Battles.Commands;
using ElsaMina.Battles.Replays;
using ElsaMina.Battles.Results;
using ElsaMina.Battles.Strategies;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.UserData;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Commands;

public class BattleStatusCommandTest
{
    private const string FORMAT = "gen9randombattle";
    private static readonly DateTimeOffset NOW = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly LadderingStatus OFF = new(null, null, false, false, false, null, 0);

    private ILadderingService _ladderingService;
    private IBattleService _battleService;
    private IBattleResultsService _battleResultsService;
    private ILadderRatingService _ladderRatingService;
    private IReplaySavingService _replaySavingService;
    private IContext _context;
    private BattleStatusCommand _command;
    private string _reply;

    [SetUp]
    public void SetUp()
    {
        _ladderingService = Substitute.For<ILadderingService>();
        _battleService = Substitute.For<IBattleService>();
        var battleDecisionManager = Substitute.For<IBattleDecisionManager>();
        _battleResultsService = Substitute.For<IBattleResultsService>();
        _ladderRatingService = Substitute.For<ILadderRatingService>();
        _replaySavingService = Substitute.For<IReplaySavingService>();
        var clockService = Substitute.For<IClockService>();
        _context = Substitute.For<IContext>();

        _ladderingService.Status.Returns(OFF);
        _battleService.GetBattlesInProgress().Returns([]);
        battleDecisionManager.ActiveStrategy.Returns(BattleDecisionStrategy.MiniPokeV2_1M);
        _battleResultsService.GetRecords().Returns([]);
        _replaySavingService.Status.Returns(new ReplaySavingStatus(false, "/bot/replays", 0, 0, 0, null));
        clockService.CurrentUtcDateTimeOffset.Returns(NOW);
        _context.When(context => context.Reply(Arg.Any<string>())).Do(callInfo => _reply = callInfo.Arg<string>());

        _command = new BattleStatusCommand(_ladderingService, _battleService, battleDecisionManager,
            _battleResultsService, _ladderRatingService, _replaySavingService, clockService);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSayItIsIdle_WhenNothingHappened()
    {
        // Act
        await _command.RunAsync(_context);

        // Assert: no format to look the ladder up for
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_reply, Does.StartWith("!code "));
            Assert.That(_reply, Does.Contain("Strategy:    minipokev2-1m"));
            Assert.That(_reply, Does.Contain("Laddering:   off"));
            Assert.That(_reply, Does.Contain("Playing:     none"));
            Assert.That(_reply, Does.Contain("no battle played since the bot started"));
            Assert.That(_reply, Does.Contain("Replays:     off, -savereplays saves them to /bot/replays"));
        }

        await _ladderRatingService.DidNotReceiveWithAnyArgs().GetRatingsAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldDescribeLadderingAndResults_WhenLaddering()
    {
        // Arrange
        _ladderingService.Status.Returns(new LadderingStatus(FORMAT, NOW.AddMinutes(-125), false, true, false,
            null, 0));
        var battle = new BattleContext("battle-gen9randombattle-9");
        battle.Tracked.Turn = 12;
        _battleService.GetBattlesInProgress().Returns([battle]);
        _battleResultsService.GetRecords().Returns(
        [
            new BattleRecord(FORMAT, BattleDecisionStrategy.MiniPokeV2_1M, 10, 5, 0),
            new BattleRecord(FORMAT, BattleDecisionStrategy.MiniPokeV1_4M, 5, 4, 1)
        ]);
        _battleResultsService.LastResult.Returns(new BattleResult("battle-gen9randombattle-8", FORMAT,
            BattleDecisionStrategy.MiniPokeV2_1M, BattleOutcome.Won, 23, NOW.AddSeconds(-250)));
        _ladderRatingService.GetRatingsAsync(Arg.Any<CancellationToken>()).Returns(
            new Dictionary<string, UserDataRankingDto>
            {
                [FORMAT] = new() { Elo = 1342.4, Gxe = 61.24, Rpr = 1620.2, Rprd = 45.1, Wins = 30, Losses = 18 }
            });
        _replaySavingService.Status.Returns(new ReplaySavingStatus(true, "/bot/replays", 24, 1, 1,
            "gen9randombattle-8"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_reply, Does.Contain("Laddering:   gen9randombattle for 2h 05m, in a battle"));
            Assert.That(_reply, Does.Contain("Playing:     battle-gen9randombattle-9 (turn 12)"));
            Assert.That(_reply, Does.Contain(
                "Ladder:      gen9randombattle: Elo 1342, GXE 61.2, Glicko 1620 ± 45, 30-18"));
            Assert.That(_reply, Does.Contain("Session:     15-9-1 (60.0% won) in 25 battles"));
            Assert.That(_reply, Does.Contain("gen9randombattle, minipokev2-1m: 10-5 (66.7% won)"));
            Assert.That(_reply, Does.Contain("gen9randombattle, minipokev1-4m: 5-4-1 (50.0% won)"));
            Assert.That(_reply, Does.Contain(
                "Last battle: won battle-gen9randombattle-8 in 23 turns with minipokev2-1m, 4m 10s ago"));
            Assert.That(_reply, Does.Contain(
                "Replays:     saving to /bot/replays (24 saved, 1 failed, 1 waiting, last gen9randombattle-8)"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldTellWhenNextSearchIsSent_WhenSearchWasNotAccepted()
    {
        // Arrange
        _ladderingService.Status.Returns(new LadderingStatus(FORMAT, NOW, false, false, false, NOW.AddSeconds(30),
            2));
        _ladderRatingService.GetRatingsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, UserDataRankingDto>());

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_reply, Does.Contain("next search in 30s (the server did not accept the last 2)"));
            Assert.That(_reply, Does.Contain("Ladder:      gen9randombattle: not rated yet"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldSayLadderIsUnavailable_WhenShowdownDoesNotAnswer()
    {
        // Arrange
        _ladderingService.Status.Returns(new LadderingStatus(FORMAT, NOW, true, false, false, null, 0));
        _ladderRatingService.GetRatingsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyDictionary<string, UserDataRankingDto>)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_reply, Does.Contain("gen9randombattle for 0s, searching for an opponent"));
            Assert.That(_reply, Does.Contain("Ladder:      unavailable, pokemonshowdown.com did not answer"));
        }
    }
}
