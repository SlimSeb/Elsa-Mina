using System.Globalization;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.President;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.President;

[TestFixture]
public class StartPresidentCommandTest
{
    private IDependencyContainerService _dependencyContainerService;
    private IArcadeEventsService _arcadeEventsService;
    private IEventAnnouncer _eventAnnouncer;
    private StartPresidentCommand _command;
    private IContext _context;
    private IRoom _room;
    private PresidentGame _game;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _eventAnnouncer = Substitute.For<IEventAnnouncer>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        var sender = MakeUser("starter");
        _context.Room.Returns(_room);
        _context.RoomId.Returns("test-room");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.Sender.Returns(sender);
        _room.Game.ReturnsNull();

        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("ElsaMina");
        configuration.Trigger.Returns("-");
        var templates = Substitute.For<ITemplatesManager>();
        templates.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(Task.FromResult(string.Empty));

        _game = new PresidentGame(Substitute.For<IRandomService>(), templates, configuration);
        _game.Context = _context;
        _dependencyContainerService.Resolve<PresidentGame>().Returns(_game);

        _command = new StartPresidentCommand(_dependencyContainerService, _arcadeEventsService, _eventAnnouncer);
    }

    private static IUser MakeUser(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return user;
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyGamesMuted_WhenGamesAreMuted()
    {
        _arcadeEventsService.AreGamesMuted("test-room").Returns(true);

        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            _context.Received(1).ReplyLocalizedMessage("games_muted_event");
            _dependencyContainerService.DidNotReceive().Resolve<PresidentGame>();
            _room.DidNotReceive().Game = Arg.Any<PresidentGame>();
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyRunning_WhenPresidentGameIsActive()
    {
        _room.Game.Returns(Substitute.For<IPresidentGame>());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("president_already_running");
        _dependencyContainerService.DidNotReceive().Resolve<PresidentGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyOtherGameRunning_WhenDifferentGameIsActive()
    {
        _room.Game.Returns(Substitute.For<IGame>());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("president_other_game_running");
        _dependencyContainerService.DidNotReceive().Resolve<PresidentGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateGame_WhenNoGameIsActive()
    {
        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            _context.Received(1).ReplyLocalizedMessage("president_game_created", Arg.Any<object>());
            _room.Received(1).Game = _game;
            Assert.That(_game.TotalRounds, Is.EqualTo(PresidentConstants.DEFAULT_ROUNDS));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetRoundCount_WhenAValidArgumentIsGiven()
    {
        _context.Target.Returns("5");

        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            _room.Received(1).Game = _game;
            Assert.That(_game.TotalRounds, Is.EqualTo(5));
        }
    }

    [TestCase("0")]
    [TestCase("11")]
    [TestCase("abc")]
    public async Task Test_RunAsync_ShouldRejectInvalidRoundCounts(string argument)
    {
        _context.Target.Returns(argument);

        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            _context.Received(1).ReplyLocalizedMessage("president_rounds_invalid", Arg.Any<object[]>());
            _room.DidNotReceive().Game = Arg.Any<PresidentGame>();
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldAnnounceGameStartToLinkedRooms_WhenNoGameIsActive()
    {
        await _command.RunAsync(_context);

        await _eventAnnouncer.Received(1).AnnounceToLinkedRoomsAsync("test-room", EventAnnounceType.Game,
            "president_started_in",
            Arg.Is<object[]>(arguments => arguments.Length == 1 && (string)arguments[0] == "test-room"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotAnnounce_WhenGamesAreMuted()
    {
        _arcadeEventsService.AreGamesMuted("test-room").Returns(true);

        await _command.RunAsync(_context);

        await _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }
}
