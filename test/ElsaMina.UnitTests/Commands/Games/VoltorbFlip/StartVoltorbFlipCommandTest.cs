using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.VoltorbFlip;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.VoltorbFlip;

public class StartVoltorbFlipCommandTest
{
    private Func<VoltorbFlipGame> _gameFactory;
    private IRoomsManager _roomsManager;
    private IVoltorbFlipGameManager _gameManager;
    private IArcadeEventsService _arcadeEventsService;
    private IConfiguration _configuration;
    private ITemplatesManager _templatesManager;
    private StartVoltorbFlipCommand _command;
    private IContext _context;
    private IRoom _room;
    private VoltorbFlipGame _game;

    [SetUp]
    public void SetUp()
    {
        _gameFactory = Substitute.For<Func<VoltorbFlipGame>>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<IVoltorbFlipGameManager>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _command = new StartVoltorbFlipCommand(_gameFactory, _roomsManager, _gameManager, _arcadeEventsService);

        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _context.Room.Returns(_room);
        _context.RoomId.Returns("test-room");

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        var randomService = Substitute.For<IRandomService>();
        randomService.RandomElement(Arg.Any<IList<(int Twos, int Threes, int Voltorbs)>>())
            .Returns((1, 0, 1));
        var dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _game = new VoltorbFlipGame(randomService, _templatesManager, _configuration, dbContextFactory);
        _gameFactory().Returns(_game);
    }

    [Test]
    public void Test_RequiredRank_ShouldReturnVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMutedEvent_WhenGamesAreMuted()
    {
        _room.Game = null;
        _arcadeEventsService.AreGamesMuted("test-room").Returns(true);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("games_muted_event");
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateGame_WhenNoGameExists()
    {
        // Arrange
        _room.Game = null;

        // Act
        await _command.RunAsync(_context);

        // Assert
        _gameFactory.Received(1)();
        Assert.That(_room.Game, Is.SameAs(_game));
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowAnnounce_WhenNoGameExists()
    {
        // Arrange
        _room.Game = null;

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), false);
        Assert.That(_game.IsRoundActive, Is.False);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyRunning_WhenOtherGameExists()
    {
        // Arrange
        _room.Game.Returns(Substitute.For<IGame>());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("vf_game_already_running");
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWaiting_WhenVoltorbFlipGameIsNotYetStarted()
    {
        // Arrange
        var existingGame = Substitute.For<IVoltorbFlipGame>();
        existingGame.IsStarted.Returns(false);
        _room.Game.Returns(existingGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("vf_game_waiting");
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoundActive_WhenVoltorbFlipGameHasActiveRound()
    {
        // Arrange
        var existingGame = Substitute.For<IVoltorbFlipGame>();
        existingGame.IsStarted.Returns(true);
        existingGame.IsRoundActive.Returns(true);
        _room.Game.Returns(existingGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("vf_game_round_active");
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartNewRound_WhenVoltorbFlipGameExistsWithNoActiveRound()
    {
        // Arrange
        var existingGame = Substitute.For<IVoltorbFlipGame>();
        existingGame.IsStarted.Returns(true);
        existingGame.IsRoundActive.Returns(false);
        _room.Game.Returns(existingGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await existingGame.Received(1).StartNewRound();
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldUpdateContext_WhenVoltorbFlipGameExistsWithNoActiveRound()
    {
        // Arrange
        var existingGame = Substitute.For<IVoltorbFlipGame>();
        existingGame.IsStarted.Returns(true);
        existingGame.IsRoundActive.Returns(false);
        _room.Game.Returns(existingGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        Assert.That(existingGame.Context, Is.SameAs(_context));
    }
}
