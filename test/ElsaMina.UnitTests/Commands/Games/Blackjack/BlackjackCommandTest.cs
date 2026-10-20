using System.Globalization;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.Blackjack;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Blackjack;

public class BlackjackCommandTest
{
    private IRoomsManager _roomsManager;
    private IBlackjackGameManager _gameManager;
    private IArcadeEventsService _arcadeEventsService;
    private ITemplatesManager _templatesManager;
    private BlackjackGame _game;
    private BlackjackCommand _command;
    private IContext _context;
    private IRoom _room;
    private IUser _sender;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<IBlackjackGameManager>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        _game = new BlackjackGame(Substitute.For<IRandomService>(), _templatesManager,
            Substitute.For<IConfiguration>());
        _command = new BlackjackCommand(() => _game, _roomsManager, _gameManager, _arcadeEventsService);

        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("player");
        _context.Sender.Returns(_sender);
        _context.Room.Returns(_room);
        _context.RoomId.Returns("arcade");
        _room.Game.ReturnsNull();
        _gameManager.GetGame(Arg.Any<string>(), Arg.Any<string>()).ReturnsNull();
    }

    [TearDown]
    public async Task TearDown()
    {
        // Stops the timeout timer of a game left in progress
        _game.Context ??= _context;
        await _game.CancelAsync();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMuted_WhenGamesAreMutedInRoom()
    {
        // Arrange
        _arcadeEventsService.AreGamesMuted("arcade").Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("games_muted_event");
        Assert.That(_room.Game, Is.Null);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyRunning_WhenBlackjackGameIsRunning()
    {
        // Arrange
        _room.Game.Returns(Substitute.For<IBlackjackGame>());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("bj_game_already_running");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyRunning_WhenAnotherGameIsRunning()
    {
        // Arrange
        _room.Game.Returns(Substitute.For<IGame>());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("bj_game_already_running");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAnnounceGameInRoom_WhenNoGameIsRunning()
    {
        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_room.Game, Is.SameAs(_game));
            Assert.That(_game.Context, Is.SameAs(_context));
        }
        await _templatesManager.Received(1).GetTemplateAsync("Games/Blackjack/BlackjackAnnounce", Arg.Any<object>());
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Test_RunAsync_ShouldReplyMissingRoom_WhenPrivateMessageHasNoRoom(string target)
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns(target);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("bj_pm_missing_room");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidRoom_WhenPrivateMessageRoomIsUnknown()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("unknown");
        _roomsManager.GetRoom("unknown").ReturnsNull();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("bj_pm_invalid_room");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyActive_WhenUserAlreadyPlaysInPrivate()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("casino");
        _roomsManager.GetRoom("casino").Returns(_room);
        _gameManager.GetGame("casino", "player").Returns(Substitute.For<IBlackjackGame>());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("bj_game_already_active");
        _gameManager.DidNotReceive().RegisterGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IBlackjackGame>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartPrivateGame_WhenPrivateMessageRoomIsValid()
    {
        // Arrange
        var culture = new CultureInfo("fr-FR");
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns(" casino ");
        _room.Culture.Returns(culture);
        _roomsManager.GetRoom("casino").Returns(_room);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().Culture = culture;
        _gameManager.Received(1).RegisterGame("casino", "player", _game);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsPrivateMode, Is.True);
            Assert.That(_game.TargetRoomId, Is.EqualTo("casino"));
            Assert.That(_game.TargetUserId, Is.EqualTo("player"));
            Assert.That(_game.Owner, Is.SameAs(_sender));
            Assert.That(_game.PlayerHand, Is.Not.Null);
        }
    }
}
