using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.FloodIt;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.FloodIt;

public class StartFloodItCommandTest
{
    private Func<FloodItGame> _gameFactory;
    private IRoomsManager _roomsManager;
    private IFloodItGameManager _gameManager;
    private IArcadeEventsService _arcadeEventsService;
    private IConfiguration _configuration;
    private ITemplatesManager _templatesManager;
    private StartFloodItCommand _command;
    private IContext _context;
    private IRoom _room;
    private FloodItGame _game;
    private IUser _sender;

    [SetUp]
    public void SetUp()
    {
        _gameFactory = Substitute.For<Func<FloodItGame>>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<IFloodItGameManager>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _command = new StartFloodItCommand(_gameFactory, _roomsManager, _gameManager, _arcadeEventsService);

        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("testplayer");
        _sender.Name.Returns("TestPlayer");
        _context.Sender.Returns(_sender);
        _context.Room.Returns(_room);
        _context.RoomId.Returns("test-room");

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        var dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var seedDb = new BotDbContext(dbOptions);
        seedDb.Database.EnsureCreated();

        var dbContextFactory = Substitute.For<IBotDbContextFactory>();
        dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(dbOptions)));

        var randomService = Substitute.For<IRandomService>();
        randomService.NextInt(Arg.Any<int>()).Returns(0);
        _configuration.Name.Returns("Bot");
        _configuration.Trigger.Returns("-");
        _game = new FloodItGame(randomService, _templatesManager, _configuration, dbContextFactory);
        _gameFactory().Returns(_game);
        _gameManager.GetGame(Arg.Any<string>(), Arg.Any<string>()).ReturnsNull();
    }

    [Test]
    public void Test_RequiredRank_ShouldReturnVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    #region Room mode

    [Test]
    public async Task Test_RunAsync_ShouldCreateGameAndShowAnnounce_WhenRoomHasNoGame()
    {
        _room.Game = null;

        await _command.RunAsync(_context);

        _gameFactory.Received(1)();
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), false);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetRoomGame_WhenRoomHasNoGame()
    {
        _room.Game = null;

        await _command.RunAsync(_context);

        Assert.That(_room.Game, Is.SameAs(_game));
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotStartRound_AfterAnnounce()
    {
        _room.Game = null;

        await _command.RunAsync(_context);

        Assert.That(_game.IsRoundActive, Is.False);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyRunning_WhenOtherGameTypeExists()
    {
        _room.Game.Returns(Substitute.For<IGame>());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("fi_game_already_running");
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWaiting_WhenFloodItGameIsNotYetStarted()
    {
        var existingGame = Substitute.For<IFloodItGame>();
        existingGame.IsStarted.Returns(false);
        _room.Game.Returns(existingGame);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("fi_game_waiting");
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoundActive_WhenFloodItGameHasActiveRound()
    {
        var existingGame = Substitute.For<IFloodItGame>();
        existingGame.IsStarted.Returns(true);
        existingGame.IsRoundActive.Returns(true);
        _room.Game.Returns(existingGame);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("fi_game_round_active");
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartNewRound_WhenFloodItGameExistsWithNoActiveRound()
    {
        var existingGame = Substitute.For<IFloodItGame>();
        existingGame.IsStarted.Returns(true);
        existingGame.IsRoundActive.Returns(false);
        _room.Game.Returns(existingGame);

        await _command.RunAsync(_context);

        await existingGame.Received(1).StartNewRound();
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldUpdateContext_WhenFloodItGameExistsWithNoActiveRound()
    {
        var existingGame = Substitute.For<IFloodItGame>();
        existingGame.IsStarted.Returns(true);
        existingGame.IsRoundActive.Returns(false);
        _room.Game.Returns(existingGame);

        await _command.RunAsync(_context);

        Assert.That(existingGame.Context, Is.SameAs(_context));
    }

    #endregion

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
    public async Task Test_RunAsync_ShouldNotCheckMute_WhenIsPrivateMessage()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("test-room");
        _roomsManager.GetRoom("test-room").Returns(_room);
        _arcadeEventsService.AreGamesMuted(Arg.Any<string>()).Returns(true);

        await _command.RunAsync(_context);

        _context.DidNotReceive().ReplyLocalizedMessage("games_muted_event");
    }

    #region Private message mode

    [Test]
    public async Task Test_RunAsync_ShouldReplyMissingRoom_WhenPmAndTargetIsEmpty()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("fi_pm_missing_room");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidRoom_WhenPmAndRoomDoesNotExist()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("unknown-room");
        _roomsManager.GetRoom("unknown-room").ReturnsNull();

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("fi_pm_invalid_room");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateAndStartGame_WhenPmAndNoExistingGame()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("test-room");
        _roomsManager.GetRoom("test-room").Returns(_room);

        await _command.RunAsync(_context);

        _gameFactory.Received(1)();
        Assert.That(_game.IsPrivateMode, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoundActive_WhenPmAndExistingGameHasActiveRound()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("test-room");
        _roomsManager.GetRoom("test-room").Returns(_room);

        var existingGame = Substitute.For<IFloodItGame>();
        existingGame.IsRoundActive.Returns(true);
        _gameManager.GetGame("test-room", "testplayer").Returns(existingGame);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("fi_game_round_active");
        await existingGame.DidNotReceive().StartNewRound();
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartNewRound_WhenPmAndExistingGameHasNoActiveRound()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("test-room");
        _roomsManager.GetRoom("test-room").Returns(_room);

        var existingGame = Substitute.For<IFloodItGame>();
        existingGame.IsRoundActive.Returns(false);
        _gameManager.GetGame("test-room", "testplayer").Returns(existingGame);

        await _command.RunAsync(_context);

        await existingGame.Received(1).StartNewRound();
    }

    #endregion
}
