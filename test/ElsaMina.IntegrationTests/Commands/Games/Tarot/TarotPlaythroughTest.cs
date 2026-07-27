using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Tarot;

/// <summary>
/// Drives complete tarot deals from the lobby to the final score against a deterministic deal, and
/// pins the whole interaction trace: which panels are posted, in which order, with which
/// <c>isChanging</c> flag, plus the templates rendered and the resource keys emitted.
/// </summary>
[TestFixture]
public class TarotPlaythroughTest
{
    private static readonly string[] FOUR_PLAYER_DISCARDS = ["2h", "3h", "4h", "5h", "6h", "7h"];
    private static readonly string[] FIVE_PLAYER_DISCARDS = ["2h", "3h", "4h"];

    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private ITarotStatsService _statsService;
    private TarotGame _game;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>(); // ShuffleInPlace is a no-op -> deterministic deal
        _configuration = Substitute.For<IConfiguration>();
        _statsService = Substitute.For<ITarotStatsService>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");

        _game = new TarotGame(_randomService, _recorder.TemplatesManager, _configuration, _statsService);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("tarot", _game.GameId);
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    [Test]
    public async Task Test_FourPlayerDeal_ShouldProduceTheExpectedInteractionTrace()
    {
        await PlayFourPlayerDealAsync();

        Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Finished));
        TraceGolden.Verify("tarot-four-players", _recorder.CompressedTrace());
    }

    [Test]
    public async Task Test_FourPlayerDeal_ShouldEndOnTheExpectedScore()
    {
        await PlayFourPlayerDealAsync();

        var result = _game.ScoreResult;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.OudlerCount, Is.EqualTo(1));
            Assert.That(result.TargetHalfPoints, Is.EqualTo(102));
            Assert.That(result.TakerHalfPoints, Is.EqualTo(72));
            Assert.That(result.Made, Is.False);
            Assert.That(result.Multiplier, Is.EqualTo(1));
            Assert.That(result.Deltas, Is.EqualTo(new[] { -240, 80, 80, 80 }));
            Assert.That(result.Deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public async Task Test_FourPlayerDeal_ShouldLogTheExpectedEventsInOrder()
    {
        await PlayFourPlayerDealAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Log[0], Is.EqualTo("tarot_taker_announced"));
            Assert.That(_game.Log[1], Is.EqualTo("tarot_dog_revealed"));
            Assert.That(_game.Log.Skip(2).Take(18), Is.All.EqualTo("tarot_trick_won"));
            Assert.That(_game.Log, Has.Count.EqualTo(20));
        }
    }

    [Test]
    public async Task Test_FourPlayerDeal_ShouldRecordTheDealInTheStatsService()
    {
        await PlayFourPlayerDealAsync();

        await _statsService.Received(1).RecordDealAsync(
            Arg.Is<IReadOnlyList<TarotPlayer>>(players => players.Count == 4),
            Arg.Any<TarotScoreResult>());
    }

    [Test]
    public async Task Test_FivePlayerDeal_ShouldProduceTheExpectedInteractionTrace()
    {
        await PlayFivePlayerDealAsync();

        Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Finished));
        TraceGolden.Verify("tarot-five-players", _recorder.CompressedTrace());
    }

    [Test]
    public async Task Test_FivePlayerDeal_ShouldEndOnTheExpectedScore()
    {
        await PlayFivePlayerDealAsync();

        var result = _game.ScoreResult;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Deltas, Has.Length.EqualTo(5));
            Assert.That(result.Deltas.Sum(), Is.Zero);
            Assert.That(_game.CalledKing, Is.EqualTo(TarotCard.Parse("ks")));
            Assert.That(_game.Partner, Is.Not.Null);
        }
    }

    /// <summary>
    /// The public panel is only ever posted fresh (a new chat message) on the very first render and on
    /// the forced re-posts; every other render updates it in place.
    /// </summary>
    [Test]
    public async Task Test_FourPlayerDeal_ShouldOnlyRepostThePublicPanelOnFreshDealsAndTricks()
    {
        await PlayFourPlayerDealAsync();

        var publicPanelCalls = _recorder.PanelTrace()
            .Where(entry => entry.StartsWith("tarot-# ", StringComparison.Ordinal))
            .Select(entry => entry.Split(' ')[1])
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(publicPanelCalls[0], Is.EqualTo("new"));
            // One forced re-post for the deal itself, then one per completed trick bar the last.
            Assert.That(publicPanelCalls.Count(call => call == "new"), Is.EqualTo(19));
            Assert.That(publicPanelCalls, Has.None.EqualTo("clear"));
        }
    }

    /// <summary>
    /// Deals four players, has player1 take on a petite, discards six low hearts and plays the deal
    /// out by always taking the first legal move.
    /// </summary>
    private async Task PlayFourPlayerDealAsync()
    {
        await JoinAndStartAsync(4);
        await BidInOrderAsync(TarotBid.Petite, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);
        await _game.DiscardAsync(_game.Taker.User, FOUR_PLAYER_DISCARDS.Select(TarotCard.Parse).ToList());
        await PlayOutAsync();
    }

    /// <summary>
    /// Deals five players, has player1 take on a petite, call the king of spades, discard three low
    /// hearts and plays the deal out by always taking the first legal move.
    /// </summary>
    private async Task PlayFivePlayerDealAsync()
    {
        await JoinAndStartAsync(5);
        await BidInOrderAsync(TarotBid.Petite, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);
        await _game.CallKingAsync(_game.Taker.User, TarotCard.Parse("ks"));
        await _game.DiscardAsync(_game.Taker.User, FIVE_PLAYER_DISCARDS.Select(TarotCard.Parse).ToList());
        await PlayOutAsync();
    }

    private async Task<IReadOnlyList<IUser>> JoinAndStartAsync(int count)
    {
        var users = GameUsers.Players(count);
        await _game.BeginJoinPhaseAsync();
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }

    private async Task BidInOrderAsync(params TarotBid[] bids)
    {
        foreach (var bid in bids)
        {
            await _game.BidAsync(_game.CurrentPlayer.User, bid);
        }
    }

    /// <summary>
    /// Plays every remaining card by always taking the first legal move, which keeps the deal fully
    /// deterministic without encoding a hand-written line of play.
    /// </summary>
    private async Task PlayOutAsync()
    {
        var safetyLimit = 200;
        while (_game.Phase == TarotPhase.Playing && safetyLimit-- > 0)
        {
            var player = _game.CurrentPlayer;
            await _game.PlayAsync(player.User, _game.GetLegalMoves(player).First());
        }

        Assert.That(safetyLimit, Is.GreaterThan(0), "the deal did not terminate");
    }
}
