using ElsaMina.Commands.Games.Cards;
using ElsaMina.Commands.Games.Belote;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Belote;

/// <summary>
/// Drives a complete belote deal from the lobby to the final score against a deterministic deal, and
/// pins the whole interaction trace: which panels are posted, in which order, with which
/// <c>isChanging</c> flag, plus the templates rendered and the resource keys emitted.
/// </summary>
[TestFixture]
public class BelotePlaythroughTest
{
    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private IBeloteStatsService _statsService;
    private BeloteGame _game;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>(); // ShuffleInPlace is a no-op -> deterministic deal
        _configuration = Substitute.For<IConfiguration>();
        _statsService = Substitute.For<IBeloteStatsService>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");

        _game = new BeloteGame(_randomService, _recorder.TemplatesManager, _configuration, _statsService);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("belote", _game.GameId);
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    [Test]
    public async Task Test_Deal_ShouldProduceTheExpectedInteractionTrace()
    {
        await PlayDealAsync();

        Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Finished));
        TraceGolden.Verify("belote-deal", _recorder.CompressedTrace());
    }

    [Test]
    public async Task Test_Deal_ShouldEndOnTheExpectedScore()
    {
        await PlayDealAsync();

        var result = _game.ScoreResult;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.TakerTeam, Is.Zero);
            Assert.That(result.Team0CardPoints + result.Team1CardPoints,
                Is.EqualTo(BeloteConstants.TOTAL_CARD_POINTS + BeloteConstants.LAST_TRICK_BONUS));
            Assert.That(_game.Team0Tricks + _game.Team1Tricks, Is.EqualTo(BeloteConstants.TRICK_COUNT));
            Assert.That(result.Deltas, Has.Length.EqualTo(4));
        }
    }

    /// <summary>
    /// The deterministic deal gives player1 the jack of diamonds as the turned card and they take it,
    /// so diamonds are trump and player1 is the taker of team 0.
    /// </summary>
    [Test]
    public async Task Test_Deal_ShouldSetTheTurnedSuitAsTrumpWhenTakenInTheFirstRound()
    {
        await PlayDealAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Trump, Is.EqualTo(Suit.Diamonds));
            Assert.That(_game.TurnedCard, Is.EqualTo(BeloteCard.Parse("jd")));
            Assert.That(_game.Players[0].IsTaker, Is.True);
            Assert.That(_game.Players[0].Team, Is.Zero);
            Assert.That(_game.Players[2].Team, Is.Zero);
            Assert.That(_game.Players[1].Team, Is.EqualTo(1));
            Assert.That(_game.Players[3].Team, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// Only the announcement of the taker reaches the room: the tricks are narrated into the game log
    /// panel instead, so a whole deal costs the room a single chat message.
    /// </summary>
    [Test]
    public async Task Test_Deal_ShouldReplyTheExpectedResourceKeysInOrder()
    {
        await PlayDealAsync();

        var replies = _recorder.EntriesOfKind("reply").Select(entry => entry["reply ".Length..]).ToList();

        Assert.That(replies, Is.EqualTo(new[] { "belote_taker_announced" }));
    }

    [Test]
    public async Task Test_Deal_ShouldLogTheExpectedEventsInOrder()
    {
        await PlayDealAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Log, Is.All.EqualTo("belote_trick_won"));
            Assert.That(_game.Log, Has.Count.EqualTo(BeloteConstants.TRICK_COUNT));
        }
    }

    [Test]
    public async Task Test_Deal_ShouldRecordTheDealInTheStatsService()
    {
        await PlayDealAsync();

        await _statsService.Received(1).RecordDealAsync(
            Arg.Is<IReadOnlyList<BelotePlayer>>(players => players.Count == 4),
            Arg.Any<BeloteScoreResult>());
    }

    private async Task PlayDealAsync()
    {
        var users = GameUsers.Players(BeloteConstants.PLAYER_COUNT);
        await _game.BeginJoinPhaseAsync();
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);

        // player1 accepts the turned card, which fixes the trump and opens the play phase.
        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, null);

        await PlayOutAsync();
    }

    /// <summary>
    /// Plays every remaining card by always taking the first legal move, which keeps the deal fully
    /// deterministic without encoding a hand-written line of play.
    /// </summary>
    private async Task PlayOutAsync()
    {
        var safetyLimit = 100;
        while (_game.Phase == BelotePhase.Playing && safetyLimit-- > 0)
        {
            var player = _game.CurrentPlayer;
            await _game.PlayAsync(player.User, _game.GetLegalMoves(player).First());
        }

        Assert.That(safetyLimit, Is.GreaterThan(0), "the deal did not terminate");
    }
}
