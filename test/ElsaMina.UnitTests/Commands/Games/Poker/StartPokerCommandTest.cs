using ElsaMina.Commands.Economy;
using ElsaMina.Commands.Games.Poker;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Poker;

public class StartPokerCommandTest
{
    private IDependencyContainerService _dependencyContainerService;
    private IEventAnnouncer _eventAnnouncer;
    private ITemplatesManager _templatesManager;
    private StartPokerCommand _command;
    private IContext _context;
    private IRoom _room;
    private PokerGame _game;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _eventAnnouncer = Substitute.For<IEventAnnouncer>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        var sender = Substitute.For<IUser>();
        sender.Name.Returns("starter");
        _context.Room.Returns(_room);
        _context.RoomId.Returns("poker-room");
        _context.Sender.Returns(sender);
        _room.Game.ReturnsNull();
        _room.GetParameterValueAsync(Parameter.BucksEnabled, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("false"));

        _game = new PokerGame(Substitute.For<IRandomService>(), _templatesManager,
            Substitute.For<IConfiguration>(), Substitute.For<IMoneyService>());
        _dependencyContainerService.Resolve<PokerGame>().Returns(_game);

        _command = new StartPokerCommand(_dependencyContainerService, _eventAnnouncer);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateGameAndAnnounceToLinkedRooms_WhenNoGameIsActive()
    {
        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_room.Game, Is.SameAs(_game));
            _context.Received(1).ReplyLocalizedMessage("poker_game_created", Arg.Any<object>(), Arg.Any<object>());
        }

        await _eventAnnouncer.Received(1).AnnounceToLinkedRoomsAsync("poker-room", EventAnnounceType.Game,
            "poker_started_in",
            Arg.Is<object[]>(arguments => arguments.Length == 1 && (string)arguments[0] == "poker-room"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotAnnounce_WhenAPokerGameIsAlreadyRunning()
    {
        _room.Game.Returns(Substitute.For<IPokerGame>());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("poker_already_running");
        await _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }
}
