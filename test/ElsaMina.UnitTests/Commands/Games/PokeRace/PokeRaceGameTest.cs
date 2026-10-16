using ElsaMina.Commands.Games.PokeRace;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.PokeRace;

public class PokeRaceGameTest
{
    private static readonly string[] FINISHED_POKEMON = ["Rapidash", "Ninjask"];

    private static readonly TimeSpan FAST_INTERVAL = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan WAIT_TIMEOUT = TimeSpan.FromSeconds(10);

    private IRandomService _randomService;
    private ITemplatesManager _templatesManager;
    private ISystemService _systemService;
    private IContext _context;
    private PokeRaceGame _game;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>();
        _randomService.RandomElement(Arg.Any<IEnumerable<RaceEvent>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<RaceEvent>>().First());
        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(callInfo => Task.FromResult($"<div>\n{callInfo.ArgAt<string>(0)}\n</div>"));
        _systemService = Substitute.For<ISystemService>();
        _systemService.SleepAsync(Arg.Any<TimeSpan>()).Returns(Task.CompletedTask);
        _context = Substitute.For<IContext>();
    }

    [TearDown]
    public void TearDown()
    {
        _game?.Cancel();
    }

    private PokeRaceGame CreateGame(TimeSpan autoStartDelay)
    {
        _game = new PokeRaceGame(_randomService, _templatesManager, autoStartDelay, FAST_INTERVAL, _systemService)
        {
            Context = _context
        };
        return _game;
    }

    private static Task WaitForEndAsync(PokeRaceGame game)
    {
        var completion = new TaskCompletionSource();
        game.GameEnded += () => completion.TrySetResult();
        return completion.Task.WaitAsync(WAIT_TIMEOUT);
    }

    [Test]
    public void Test_Identifier_ShouldReturnPokeRaceGame()
    {
        Assert.That(CreateGame(TimeSpan.FromHours(1)).Identifier, Is.EqualTo("PokeRaceGame"));
    }

    [Test]
    public async Task Test_BeginJoinPhaseAsync_ShouldDisplayLobby()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));

        // Act
        await game.BeginJoinPhaseAsync();

        // Assert
        _context.Received(1).SendUpdatableHtml($"pokerace-{game.GameId}",
            "<div>Games/PokeRace/PokeRaceLobby</div>", false);
    }

    [Test]
    public async Task Test_JoinRaceAsync_ShouldRegisterPlayerAndRefreshLobby_WhenPokemonIsFree()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));

        // Act
        var (success, messageKey, args) = await game.JoinRaceAsync("Some User", "Rapidash");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(messageKey, Is.EqualTo("pokerace_join_success"));
            Assert.That(args, Is.EqualTo(new object[] { "Some User", "Rapidash" }));
            Assert.That(game.Players["someuser"], Is.EqualTo(("Some User", "Rapidash")));
        }
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), true);
    }

    [Test]
    public async Task Test_JoinRaceAsync_ShouldFail_WhenPlayerAlreadyChoseAPokemon()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));
        await game.JoinRaceAsync("Player", "Rapidash");

        // Act
        var (success, messageKey, args) = await game.JoinRaceAsync("Player", "Jolteon");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("pokerace_join_already_chosen"));
            Assert.That(args, Is.EqualTo(new object[] { "Rapidash" }));
        }
    }

    [Test]
    public async Task Test_JoinRaceAsync_ShouldFail_WhenPokemonIsUnknown()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));

        // Act
        var (success, messageKey, args) = await game.JoinRaceAsync("Player", "Slowpoke");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("pokerace_join_invalid_pokemon"));
            Assert.That(args[0], Is.EqualTo("Slowpoke"));
            Assert.That((string)args[1], Does.Contain("Rapidash"));
        }
    }

    [Test]
    public async Task Test_JoinRaceAsync_ShouldFail_WhenPokemonIsTaken()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));
        await game.JoinRaceAsync("Player1", "Rapidash");

        // Act
        var (success, messageKey, _) = await game.JoinRaceAsync("Player2", "Rapidash");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("pokerace_join_pokemon_taken"));
        }
    }

    [Test]
    public async Task Test_JoinRaceAsync_ShouldFail_WhenRaceAlreadyStarted()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));
        await game.JoinRaceAsync("Player1", "Rapidash");
        await game.JoinRaceAsync("Player2", "Jolteon");
        await game.StartRaceAsync();

        // Act
        var (success, messageKey, _) = await game.JoinRaceAsync("Player3", "Crobat");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("pokerace_race_already_started"));
        }
    }

    [Test]
    public async Task Test_StartRaceAsync_ShouldDoNothing_WhenNotEnoughPlayers()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));
        await game.JoinRaceAsync("Player1", "Rapidash");

        // Act
        await game.StartRaceAsync();

        // Assert
        Assert.That(game.IsStarted, Is.False);
    }

    [Test]
    public async Task Test_StartRaceAsync_ShouldRunRaceUntilEveryRacerFinishes()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));
        await game.JoinRaceAsync("Player1", "Rapidash");
        await game.JoinRaceAsync("Player2", "Ninjask");
        var raceEnded = WaitForEndAsync(game);
        PokeRaceModel lastModel = null;
        _templatesManager
            .When(manager => manager.GetTemplateAsync("Games/PokeRace/PokeRaceResults", Arg.Any<object>()))
            .Do(callInfo => lastModel = callInfo.ArgAt<PokeRaceModel>(1));
        // Events trigger on every turn
        _randomService.NextDouble().Returns(0.1);

        // Act
        await game.StartRaceAsync();
        await raceEnded;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.IsEnded, Is.True);
            Assert.That(lastModel, Is.Not.Null);
            Assert.That(lastModel.Finished, Is.EquivalentTo(FINISHED_POKEMON));
            Assert.That(lastModel.SortedPositions.All(position => position.Position >= PokeRaceConstants.RACE_LENGTH),
                Is.True);
            Assert.That(lastModel.AllEvents, Is.Not.Empty);
            Assert.That(lastModel.AllEvents, Has.Count.LessThanOrEqualTo(PokeRaceConstants.MAX_RECENT_EVENTS));
            Assert.That(lastModel.Turn, Is.GreaterThan(0));
        }
        await _systemService.Received(1).SleepAsync(TimeSpan.FromSeconds(3));
        _context.Received().SendUpdatableHtml(Arg.Any<string>(), "<div>Games/PokeRace/PokeRaceStart</div>", true);
        _context.Received().SendUpdatableHtml(Arg.Any<string>(), "<div>Games/PokeRace/PokeRaceUpdate</div>", true);
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), "<div>Games/PokeRace/PokeRaceResults</div>", true);
    }

    [Test]
    public async Task Test_StartRaceAsync_ShouldNotStartUpdates_WhenCancelledDuringCountdown()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));
        await game.JoinRaceAsync("Player1", "Rapidash");
        await game.JoinRaceAsync("Player2", "Ninjask");
        var countdown = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>()).Returns(countdown.Task);

        // Act
        await game.StartRaceAsync();
        game.Cancel();
        countdown.SetResult();
        await Task.Delay(50);

        // Assert
        await _templatesManager.DidNotReceive().GetTemplateAsync("Games/PokeRace/PokeRaceUpdate", Arg.Any<object>());
    }

    [Test]
    public async Task Test_AutoStart_ShouldCancelRace_WhenNotEnoughPlayersJoined()
    {
        // Arrange
        var game = CreateGame(FAST_INTERVAL);
        await game.JoinRaceAsync("Player1", "Rapidash");
        var raceEnded = WaitForEndAsync(game);

        // Act
        await game.BeginJoinPhaseAsync();
        await raceEnded;

        // Assert
        _context.Received(1).ReplyLocalizedMessage("pokerace_auto_start_not_enough_players",
            PokeRaceConstants.MIN_PLAYERS);
    }

    [Test]
    public async Task Test_AutoStart_ShouldStartRace_WhenEnoughPlayersJoined()
    {
        // Arrange
        var game = CreateGame(FAST_INTERVAL);
        await game.JoinRaceAsync("Player1", "Rapidash");
        await game.JoinRaceAsync("Player2", "Ninjask");
        _randomService.NextDouble().Returns(0.99);
        var raceEnded = WaitForEndAsync(game);

        // Act
        await game.BeginJoinPhaseAsync();
        await raceEnded;

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Games/PokeRace/PokeRaceResults", Arg.Any<object>());
    }

    [Test]
    public void Test_Cancel_ShouldEndGame()
    {
        // Arrange
        var game = CreateGame(TimeSpan.FromHours(1));

        // Act
        game.Cancel();

        // Assert
        Assert.That(game.IsEnded, Is.True);
    }
}
