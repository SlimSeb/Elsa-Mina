using ElsaMina.Commands.Games.President;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.President;

/// <summary>
/// Drives a complete président game (two rounds, so the card exchange of the second round is covered)
/// from the lobby to the final standings against a deterministic deal, and pins the whole interaction
/// trace: which panels are posted, in which order, with which <c>isChanging</c> flag.
/// </summary>
[TestFixture]
public class PresidentPlaythroughTest
{
    private const int PLAYER_COUNT = 4;
    private const int ROUNDS = 2;

    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private PresidentGame _game;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>(); // ShuffleInPlace is a no-op -> deterministic deal
        _configuration = Substitute.For<IConfiguration>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");

        _game = new PresidentGame(_randomService, _recorder.TemplatesManager, _configuration);
        _game.Context = _recorder.Context;
        _game.TotalRounds = ROUNDS;
        _recorder.MaskGameId("president", _game.GameId);
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    [Test]
    public async Task Test_TwoRoundGame_ShouldProduceTheExpectedInteractionTrace()
    {
        await PlayGameAsync();

        Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Finished));
        TraceGolden.Verify("president-two-rounds", _recorder.CompressedTrace());
    }

    [Test]
    public async Task Test_TwoRoundGame_ShouldEndOnACompleteFinishOrder()
    {
        await PlayGameAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.RoundNumber, Is.EqualTo(ROUNDS));
            Assert.That(_game.FinishOrder, Has.Count.EqualTo(PLAYER_COUNT));
            Assert.That(_game.FinishOrder.Select(player => player.FinishPosition),
                Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(_game.Players.Select(player => player.Role),
                Has.Exactly(1).EqualTo(PresidentRole.President));
            Assert.That(_game.Players.Select(player => player.Role),
                Has.Exactly(1).EqualTo(PresidentRole.Scum));
            // Every round hands out (playerCount - 1) + ... + 0 points in total.
            Assert.That(_game.Players.Sum(player => player.Score), Is.EqualTo(ROUNDS * 6));
        }
    }

    /// <summary>
    /// The exchange of the second round is forced for the scum and the vice-scum, so both give-backs
    /// have to be logged before play resumes.
    /// </summary>
    [Test]
    public async Task Test_TwoRoundGame_ShouldLogBothHalvesOfTheExchange()
    {
        await PlayGameAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Log.Count(entry => entry == "president_exchange_gave_best"), Is.EqualTo(2));
            Assert.That(_game.Log.Count(entry => entry == "president_exchange_returned"), Is.EqualTo(2));
            Assert.That(_game.Log.Count(entry => entry == "president_round_started"), Is.EqualTo(ROUNDS));
            Assert.That(_game.Log.Count(entry => entry == "president_round_ended"), Is.EqualTo(ROUNDS));
        }
    }

    private async Task PlayGameAsync()
    {
        var users = GameUsers.Players(PLAYER_COUNT);
        await _game.BeginJoinPhaseAsync();
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        await PlayOutAsync();
    }

    /// <summary>
    /// Plays the whole game out mechanically: during the exchange every debtor hands over their lowest
    /// cards, and while playing each player takes their first legal combination or passes. That keeps
    /// the game fully deterministic without encoding a hand-written line of play.
    /// </summary>
    private async Task PlayOutAsync()
    {
        var safetyLimit = 400;
        while (_game.Phase != PresidentPhase.Finished && safetyLimit-- > 0)
        {
            if (_game.Phase == PresidentPhase.Exchange)
            {
                await ResolveExchangeAsync();
                continue;
            }

            var player = _game.CurrentPlayer;
            if (player is null)
            {
                break;
            }

            var legalPlays = _game.GetLegalPlays(player);
            if (legalPlays.Count > 0)
            {
                var (rank, count) = legalPlays[0];
                await _game.PlayAsync(player.User, rank, count);
                continue;
            }

            await _game.PassAsync(player.User);
        }

        Assert.That(safetyLimit, Is.GreaterThan(0), "the game did not terminate");
    }

    private async Task ResolveExchangeAsync()
    {
        foreach (var player in _game.Players.Where(currentPlayer => currentPlayer.CardsToGive > 0).ToList())
        {
            var lowestCards = player.Hand.OrderBy(card => card.Rank).Take(player.CardsToGive).ToList();
            await _game.GiveAsync(player.User, lowestCards);
        }
    }
}
