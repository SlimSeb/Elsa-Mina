using ElsaMina.Commands.Games.Chess;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Chess;

public class ChessGameTest
{
    private ChessGame _game;
    private IRandomService _mockRandomService;
    private ITemplatesManager _mockTemplatesManager;
    private IConfiguration _configuration;
    private IDependencyContainerService _dependencyContainerService;
    private IChessRatingService _mockRatingService;
    private IContext _context;
    private IUser _mockUser1;
    private IUser _mockUser2;

    [SetUp]
    public void SetUp()
    {
        _mockRandomService = Substitute.For<IRandomService>();
        _mockTemplatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _mockRatingService = Substitute.For<IChessRatingService>();

        DependencyContainerService.Current = _dependencyContainerService;

        _configuration.Name.Returns("Bot");
        _configuration.Trigger.Returns("!");
        _configuration.DefaultLocaleCode.Returns("fr-FR");
        _mockTemplatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));
        _mockRatingService.UpdateRatingsOnWinAsync(Arg.Any<IUser>(), Arg.Any<IUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ChessRatingChange, ChessRatingChange)>(
                (new ChessRatingChange(1000, 1016), new ChessRatingChange(1000, 984))));
        _mockRatingService.UpdateRatingsOnDrawAsync(Arg.Any<IUser>(), Arg.Any<IUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ChessRatingChange, ChessRatingChange)>(
                (new ChessRatingChange(1000, 1000), new ChessRatingChange(1000, 1000))));
        _game = new ChessGame(_mockRandomService, _mockTemplatesManager, _configuration,
            _mockRatingService, ChessConstants.TIMEOUT_DELAY);
        _game.Context = _context;

        _mockUser1 = Substitute.For<IUser>();
        _mockUser2 = Substitute.For<IUser>();
        _mockUser1.Name.Returns("Player1");
        _mockUser2.Name.Returns("Player2");
    }

    [TearDown]
    public void TearDown()
    {
        DependencyContainerService.Current = null;
    }

    [Test]
    public async Task Test_JoinGame_ShouldAddPlayersAndStart_WhenTwoPlayersJoin()
    {
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Players, Has.Member(_mockUser1));
            Assert.That(_game.Players, Has.Member(_mockUser2));
            Assert.That(_game.Players, Has.Count.EqualTo(2));
            Assert.That(_game.IsStarted, Is.True);
            Assert.That(_game.WhitePlayer, Is.EqualTo(_mockUser1));
            Assert.That(_game.PlayerCurrentlyPlaying, Is.EqualTo(_mockUser1));
        }
    }

    [Test]
    public async Task Test_Play_ShouldRejectMove_WhenNotPlayersTurn()
    {
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        await _game.Play(_mockUser2, "e7e5"); // Black tries to move first

        Assert.That(_game.Board.Squares[1, 4], Is.EqualTo('p')); // Pawn unmoved
    }

    [Test]
    public async Task Test_Play_ShouldMakeMove_WhenInputIsValid()
    {
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        await _game.Play(_mockUser1, "e2e4");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Board.Squares[4, 4], Is.EqualTo('P'));
            Assert.That(_game.Board.Squares[6, 4], Is.EqualTo(ChessBoard.EMPTY));
            Assert.That(_game.TurnCount, Is.EqualTo(1));
            Assert.That(_game.PlayerCurrentlyPlaying, Is.EqualTo(_mockUser2));
        }
    }

    [Test]
    public async Task Test_Play_ShouldSelectPiece_WhenInputIsSquare()
    {
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        await _game.Play(_mockUser1, "e2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.SelectedSquare, Is.EqualTo((6, 4)));
            Assert.That(_game.SelectedSquareDestinations, Does.Contain((5, 4)));
            Assert.That(_game.SelectedSquareDestinations, Does.Contain((4, 4)));
        }
    }

    [Test]
    public async Task Test_Play_ShouldDeselect_WhenSameSquareSelectedTwice()
    {
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        await _game.Play(_mockUser1, "e2");
        await _game.Play(_mockUser1, "e2");

        Assert.That(_game.SelectedSquare, Is.Null);
    }

    [Test]
    public async Task Test_Game_ShouldDeclareWinner_OnCheckmate()
    {
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        // Fool's mate: Black checkmates White.
        await _game.Play(_mockUser1, "f2f3");
        await _game.Play(_mockUser2, "e7e5");
        await _game.Play(_mockUser1, "g2g4");
        await _game.Play(_mockUser2, "d8h4");

        using (Assert.EnterMultipleScope())
        {
            _context.Received(1).ReplyLocalizedMessage("chess_game_win_message", _mockUser2.Name);
            Assert.That(_game.IsEnded, Is.True);
        }
        await _mockRatingService.Received(1)
            .UpdateRatingsOnWinAsync(_mockUser2, _mockUser1, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_Forfeit_ShouldDeclareOpponentWinner()
    {
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        await _game.Forfeit(_mockUser1);

        _context.Received(1).ReplyLocalizedMessage("chess_game_player_forfeited", _mockUser1.Name);
        _context.Received(1).ReplyLocalizedMessage("chess_game_win_message", _mockUser2.Name);
    }

    [Test]
    public async Task Test_OnTimeout_ShouldDisqualifyCurrentPlayer()
    {
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        await _game.OnTimeout();

        _context.Received(1).ReplyLocalizedMessage("chess_game_on_timeout", _mockUser1.Name);
        _context.Received(1).ReplyLocalizedMessage("chess_game_win_message", _mockUser2.Name);
    }

    [Test]
    public async Task Test_Timeout_ShouldDisqualifyPlayer_WhenDelayElapses()
    {
        var game = CreateGameWithTimeout(TimeSpan.FromMilliseconds(40));
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);

        await Task.Delay(120);

        _context.Received(1).ReplyLocalizedMessage("chess_game_on_timeout", _mockUser1.Name);
    }

    [Test]
    public async Task Test_Timeout_ShouldBeCanceled_WhenGameIsCanceled()
    {
        var game = CreateGameWithTimeout(TimeSpan.FromMilliseconds(40));
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);

        game.Cancel();
        await Task.Delay(120);

        _context.DidNotReceive().ReplyLocalizedMessage("chess_game_on_timeout", Arg.Any<string>());
    }

    [Test]
    public async Task Test_JoinGame_ShouldNotExceedMaxPlayers_WhenMultiplePlayersJoinConcurrently()
    {
        var mockUser3 = Substitute.For<IUser>();
        mockUser3.Name.Returns("Player3");

        await Task.WhenAll(
            _game.JoinGame(_mockUser1),
            _game.JoinGame(_mockUser2),
            _game.JoinGame(mockUser3)
        );

        Assert.That(_game.Players, Has.Count.EqualTo(ChessConstants.MAX_PLAYERS_COUNT));
    }

    private ChessGame CreateGameWithTimeout(TimeSpan timeoutDelay)
    {
        var ratingService = Substitute.For<IChessRatingService>();
        ratingService.UpdateRatingsOnWinAsync(Arg.Any<IUser>(), Arg.Any<IUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ChessRatingChange, ChessRatingChange)>(
                (new ChessRatingChange(1000, 1016), new ChessRatingChange(1000, 984))));
        var game = new ChessGame(_mockRandomService, _mockTemplatesManager, _configuration,
            ratingService, timeoutDelay);
        game.Context = _context;
        return game;
    }
}
