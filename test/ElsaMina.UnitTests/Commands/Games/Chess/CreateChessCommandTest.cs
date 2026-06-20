using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.Chess;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Chess;

public class CreateChessCommandTest
{
    private IDependencyContainerService _dependencyContainerService;
    private IArcadeEventsService _arcadeEventsService;
    private IConfiguration _configuration;
    private CreateChessCommand _command;
    private IContext _context;
    private IRoom _room;
    private ITemplatesManager _templatesManager;
    private ChessGame _game;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _command = new CreateChessCommand(_dependencyContainerService, _arcadeEventsService);

        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _room.RoomId.Returns("room-id");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));
        _game = new ChessGame(Substitute.For<IRandomService>(), _templatesManager,
            _configuration, Substitute.For<IChessRatingService>(), ChessConstants.INITIAL_CLOCK);

        _context.RoomId.Returns("room-id");
        _context.Room.Returns(_room);
        _dependencyContainerService.Resolve<ChessGame>().Returns(_game);
    }

    [Test]
    public async Task Test_RunAsync_ShouldAnnounceGameStart_WhenNoGameAlreadyExists()
    {
        _room.Game = null;
        _configuration.Trigger.Returns("!");

        await _command.RunAsync(_context);

        _dependencyContainerService.Received(1).Resolve<ChessGame>();
        await _templatesManager.GetTemplateAsync("Games/Chess/ChessGamePanel", Arg.Any<object>());
        Assert.That(_room.Game, Is.SameAs(_game));
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotStartGame_WhenGameAlreadyExists()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("chess_game_start_already_exist");
        _dependencyContainerService.DidNotReceive().Resolve<ChessGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotStartGame_WhenGamesAreMuted()
    {
        _room.Game = null;
        _arcadeEventsService.AreGamesMuted("room-id").Returns(true);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("games_muted_event");
        _dependencyContainerService.DidNotReceive().Resolve<ChessGame>();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }
}
