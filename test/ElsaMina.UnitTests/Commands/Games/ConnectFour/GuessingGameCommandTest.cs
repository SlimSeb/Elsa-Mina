using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.GuessingGame;
using ElsaMina.Commands.Games.GuessingGame.Trivia;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.ConnectFour;

public class GuessingGameCommandTest
{
    private GuessingGameCommand _command;
    private IDependencyContainerService _dependencyContainerService;
    private IArcadeEventsService _arcadeEventsService;
    private IEventAnnouncer _eventAnnouncer;
    private IContext _context;
    private IRoom _room;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _eventAnnouncer = Substitute.For<IEventAnnouncer>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        _command = new GuessingGameCommand(_dependencyContainerService, _arcadeEventsService, _eventAnnouncer);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyGamesMutedMessage_WhenGamesAreMuted()
    {
        // Arrange
        _context.RoomId.Returns("testroom");
        _arcadeEventsService.AreGamesMuted("testroom").Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("games_muted_event");
        _context.DidNotReceive().ReplyLocalizedMessage("guessing_game_specify");
        await _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySpecifyMessage_WhenTurnsCountIsInvalid()
    {
        // Arrange
        _context.Target.Returns("invalid");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("guessing_game_specify");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidTurnsMessage_WhenTurnsCountIsOutOfRange()
    {
        // Arrange
        _context.Target.Returns("25");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("guessing_game_invalid_number_turns", 20);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyOngoingGameMessage_WhenGameIsAlreadyRunning()
    {
        // Arrange
        _context.Target.Returns("10");
        _context.Room.Returns(_room);
        _room.Game.Returns(Substitute.For<IGame>());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("guessing_game_currently_ongoing");
    }

    [Test]
    public async Task Test_RunAsync_ShouldResolveTriviaGame_WhenCommandIsTrivia()
    {
        // Arrange
        _context.Target.Returns("5");
        _context.Command.Returns("trivia");
        _context.RoomId.Returns("testroom");
        _room.Game.ReturnsNull();
        _context.Room.Returns(_room);
        var triviaGame = Substitute.For<TriviaGame>(
            Substitute.For<ITriviaService>(),
            Substitute.For<ITemplatesManager>(),
            Substitute.For<IConfiguration>(),
            Substitute.For<IClockService>());
        _dependencyContainerService.Resolve<TriviaGame>().Returns(triviaGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _dependencyContainerService.Received(1).Resolve<TriviaGame>();
        _room.Received(1).Game = triviaGame;
        Assert.That(triviaGame.TurnsCount, Is.EqualTo(5));
        Assert.That(triviaGame.Context, Is.EqualTo(_context));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidCommand_WhenCommandIsUnknown()
    {
        // Arrange
        _context.Target.Returns("5");
        _context.Command.Returns("unknown");
        _room.Game.ReturnsNull();
        _context.Room.Returns(_room);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("guessing_game_invalid_command");
        await _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }
}