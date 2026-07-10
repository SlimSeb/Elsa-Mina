using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.Battleship;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Battleship;

public class CreateBattleshipCommandTest
{
    private IDependencyContainerService _dependencyContainerService;
    private IArcadeEventsService _arcadeEventsService;
    private IEventAnnouncer _eventAnnouncer;
    private ITemplatesManager _templatesManager;
    private CreateBattleshipCommand _command;
    private IContext _context;
    private IRoom _room;
    private BattleshipGame _game;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _eventAnnouncer = Substitute.For<IEventAnnouncer>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        _context.Room.Returns(_room);
        _context.RoomId.Returns("room-id");

        _game = new BattleshipGame(Substitute.For<IRandomService>(), _templatesManager,
            Substitute.For<IConfiguration>(), Substitute.For<IBattleshipRatingService>());
        _game.Context = _context;
        _dependencyContainerService.Resolve<BattleshipGame>().Returns(_game);

        _command = new CreateBattleshipCommand(_dependencyContainerService, _arcadeEventsService, _eventAnnouncer);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateGameAndAnnounceToLinkedRooms_WhenNoGameAlreadyExists()
    {
        _room.Game = null;

        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_room.Game, Is.SameAs(_game));
        }

        await _eventAnnouncer.Received(1).AnnounceToLinkedRoomsAsync("room-id", EventAnnounceType.Game,
            "battleship_started_in",
            Arg.Is<object[]>(arguments => arguments.Length == 1 && (string)arguments[0] == "room-id"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotAnnounce_WhenGamesAreMuted()
    {
        _room.Game = null;
        _arcadeEventsService.AreGamesMuted("room-id").Returns(true);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("games_muted_event");
        await _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotAnnounce_WhenGameAlreadyExists()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("battleship_game_start_already_exist");
        await _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }
}
