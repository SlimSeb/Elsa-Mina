using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.LightsOut;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.LightsOut;

[TestFixture]
public class StartLightsOutCommandTest
{
    private IDependencyContainerService _dependencyContainerService;
    private IRoomsManager _roomsManager;
    private ILightsOutGameManager _gameManager;
    private IArcadeEventsService _arcadeEventsService;
    private IContext _context;
    private IRoom _room;
    private LightsOutGame _game;
    private StartLightsOutCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<ILightsOutGameManager>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();

        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _context.Room.Returns(_room);
        _context.RoomId.Returns("room1");

        var templatesManager = Substitute.For<ITemplatesManager>();
        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));
        var configuration = Substitute.For<IConfiguration>();
        var randomService = Substitute.For<IRandomService>();
        randomService.NextInt(Arg.Any<int>()).Returns(0);

        var dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using (var db = new BotDbContext(dbOptions)) db.Database.EnsureCreated();
        var dbContextFactory = Substitute.For<IBotDbContextFactory>();
        dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(dbOptions)));

        _game = new LightsOutGame(randomService, templatesManager, configuration, dbContextFactory);
        _dependencyContainerService.Resolve<LightsOutGame>().Returns(_game);

        _sut = new StartLightsOutCommand(_dependencyContainerService, _roomsManager, _gameManager, _arcadeEventsService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_sut.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_sut.IsAllowedInPrivateMessage, Is.True);
    }

    // --- Room message path ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyMuted_WhenGamesAreMuted()
    {
        _context.IsPrivateMessage.Returns(false);
        _room.Game = null;
        _arcadeEventsService.AreGamesMuted("room1").Returns(true);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("games_muted_event");
        _dependencyContainerService.DidNotReceive().Resolve<LightsOutGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateGame_WhenNoGameExists()
    {
        _context.IsPrivateMessage.Returns(false);
        _room.Game = null;
        _arcadeEventsService.AreGamesMuted("room1").Returns(false);

        await _sut.RunAsync(_context);

        _dependencyContainerService.Received(1).Resolve<LightsOutGame>();
        Assert.That(_room.Game, Is.SameAs(_game));
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowAnnounce_WhenNoGameExists()
    {
        _context.IsPrivateMessage.Returns(false);
        _room.Game = null;
        _arcadeEventsService.AreGamesMuted("room1").Returns(false);

        await _sut.RunAsync(_context);

        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), false);
        Assert.That(_game.IsRoundActive, Is.False);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyRunning_WhenOtherGameExists()
    {
        _context.IsPrivateMessage.Returns(false);
        _room.Game.Returns(Substitute.For<IGame>());
        _arcadeEventsService.AreGamesMuted("room1").Returns(false);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_game_already_running");
        _dependencyContainerService.DidNotReceive().Resolve<LightsOutGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWaiting_WhenLightsOutGameNotYetStarted()
    {
        _context.IsPrivateMessage.Returns(false);
        _arcadeEventsService.AreGamesMuted("room1").Returns(false);
        var existingGame = Substitute.For<ILightsOutGame>();
        existingGame.IsStarted.Returns(false);
        _room.Game.Returns(existingGame);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_game_waiting");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoundActive_WhenLightsOutHasActiveRound()
    {
        _context.IsPrivateMessage.Returns(false);
        _arcadeEventsService.AreGamesMuted("room1").Returns(false);
        var existingGame = Substitute.For<ILightsOutGame>();
        existingGame.IsStarted.Returns(true);
        existingGame.IsRoundActive.Returns(true);
        _room.Game.Returns(existingGame);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_game_round_active");
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartNewRound_WhenLightsOutExistsWithNoActiveRound()
    {
        _context.IsPrivateMessage.Returns(false);
        _arcadeEventsService.AreGamesMuted("room1").Returns(false);
        var existingGame = Substitute.For<ILightsOutGame>();
        existingGame.IsStarted.Returns(true);
        existingGame.IsRoundActive.Returns(false);
        _room.Game.Returns(existingGame);

        await _sut.RunAsync(_context);

        await existingGame.Received(1).StartNewRound();
        _dependencyContainerService.DidNotReceive().Resolve<LightsOutGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldUpdateOwnerAndContext_WhenContinuingExistingGame()
    {
        _context.IsPrivateMessage.Returns(false);
        _arcadeEventsService.AreGamesMuted("room1").Returns(false);
        var sender = Substitute.For<IUser>();
        _context.Sender.Returns(sender);
        var existingGame = Substitute.For<ILightsOutGame>();
        existingGame.IsStarted.Returns(true);
        existingGame.IsRoundActive.Returns(false);
        _room.Game.Returns(existingGame);

        await _sut.RunAsync(_context);

        Assert.That(existingGame.Owner, Is.SameAs(sender));
        Assert.That(existingGame.Context, Is.SameAs(_context));
    }

    // --- Private message path ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyMissingRoom_WhenPmWithEmptyTarget()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns(string.Empty);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_pm_missing_room");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidRoom_WhenPmRoomNotFound()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("unknownroom");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_pm_invalid_room");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreatePrivateGame_WhenPmWithValidRoom()
    {
        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("user1");
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("room1");
        _context.Sender.Returns(sender);
        _roomsManager.GetRoom("room1").Returns(_room);
        _gameManager.GetGame("room1", "user1").Returns((ILightsOutGame)null);

        await _sut.RunAsync(_context);

        _dependencyContainerService.Received(1).Resolve<LightsOutGame>();
        _gameManager.Received(1).RegisterGame("room1", "user1", _game);
        Assert.That(_game.IsPrivateMode, Is.True);
        Assert.That(_game.TargetRoomId, Is.EqualTo("room1"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoundActive_WhenPmAndExistingRoundActive()
    {
        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("user1");
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("room1");
        _context.Sender.Returns(sender);
        _roomsManager.GetRoom("room1").Returns(_room);
        var existingGame = Substitute.For<ILightsOutGame>();
        existingGame.IsRoundActive.Returns(true);
        _gameManager.GetGame("room1", "user1").Returns(existingGame);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_game_round_active");
        await existingGame.DidNotReceive().StartNewRound();
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartNewRound_WhenPmAndExistingGameWithNoActiveRound()
    {
        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("user1");
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("room1");
        _context.Sender.Returns(sender);
        _roomsManager.GetRoom("room1").Returns(_room);
        var existingGame = Substitute.For<ILightsOutGame>();
        existingGame.IsRoundActive.Returns(false);
        _gameManager.GetGame("room1", "user1").Returns(existingGame);

        await _sut.RunAsync(_context);

        await existingGame.Received(1).StartNewRound();
        _dependencyContainerService.DidNotReceive().Resolve<LightsOutGame>();
    }
}
