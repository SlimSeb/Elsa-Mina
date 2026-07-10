using ElsaMina.Commands.Games.ConnectFour;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.ConnectFour;

public class CreateConnectFourCommandTest
{
    private IDependencyContainerService _dependencyContainerService;
    private IConfiguration _configuration;
    private IEventAnnouncer _eventAnnouncer;
    private CreateConnectFourCommand _command;
    private IContext _context;
    private IRoom _room;
    private ITemplatesManager _templatesManager;
    private ConnectFourGame _game;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _configuration = Substitute.For<IConfiguration>();
        _eventAnnouncer = Substitute.For<IEventAnnouncer>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _command = new CreateConnectFourCommand(_dependencyContainerService, _eventAnnouncer);

        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _room.RoomId.Returns("room-id");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));
        _game = new ConnectFourGame(Substitute.For<IRandomService>(), _templatesManager,
            _configuration, Substitute.For<IConnectFourRatingService>(), ConnectFourConstants.TIMEOUT_DELAY);

        _context.RoomId.Returns("room-id");
        _context.Room.Returns(_room);
        _dependencyContainerService.Resolve<ConnectFourGame>().Returns(_game);
    }

    [Test]
    public async Task Test_RunAsync_ShouldAnnounceGameStart_WhenNoGameAlreadyExists()
    {
        // Arrange
        _room.Game = null;
        _configuration.Trigger.Returns("!");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _dependencyContainerService.Received(1).Resolve<ConnectFourGame>();
        await _templatesManager.GetTemplateAsync("Games/ConnectFour/ConnectFourGamePanel", Arg.Any<object>());
        Assert.That(_room.Game, Is.SameAs(_game));
    }

    [Test]
    public async Task Test_RunAsync_ShouldAnnounceGameStartToLinkedRooms_WhenNoGameAlreadyExists()
    {
        // Arrange
        _room.Game = null;
        _configuration.Trigger.Returns("!");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _eventAnnouncer.Received(1).AnnounceToLinkedRoomsAsync("room-id", EventAnnounceType.Game,
            "connect_four_started_in",
            Arg.Is<object[]>(arguments => arguments.Length == 1 && (string)arguments[0] == "room-id"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotStartGame_WhenGameAlreadyExists()
    {
        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("c4_game_start_already_exist");
        _dependencyContainerService.DidNotReceive().Resolve<ConnectFourGame>();
        await _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public void Test_RequiredRank_ShouldReturnCorrectRank()
    {
        // Act
        var rank = _command.RequiredRank;

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Voiced));
    }
}
