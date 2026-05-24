using ElsaMina.Commands.Games.ConnectFour;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.ConnectFour;

public class ConnectFourGameTest
{
    private ConnectFourGame _game;
    private IRandomService _mockRandomService;
    private ITemplatesManager _mockTemplatesManager;
    private IConfiguration _configuration;
    private IDependencyContainerService _dependencyContainerService;
    private IConnectFourRatingService _mockRatingService;
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
        _mockRatingService = Substitute.For<IConnectFourRatingService>();

        DependencyContainerService.Current = _dependencyContainerService;

        _configuration.Name.Returns("Bot");
        _configuration.Trigger.Returns("!");
        _configuration.DefaultLocaleCode.Returns("fr-FR");
        _mockTemplatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));
        _mockRatingService.UpdateRatingsOnWinAsync(Arg.Any<IUser>(), Arg.Any<IUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ConnectFourRatingChange, ConnectFourRatingChange)>(
                (new ConnectFourRatingChange(1000, 1016), new ConnectFourRatingChange(1000, 984))));
        _mockRatingService.UpdateRatingsOnDrawAsync(Arg.Any<IUser>(), Arg.Any<IUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ConnectFourRatingChange, ConnectFourRatingChange)>(
                (new ConnectFourRatingChange(1000, 1000), new ConnectFourRatingChange(1000, 1000))));
        _game = new ConnectFourGame(_mockRandomService, _mockTemplatesManager, _configuration,
            _mockRatingService, ConnectFourConstants.TIMEOUT_DELAY);
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
    public async Task Test_JoinGame_ShouldAddPlayers_WhenGameIsNotStarted()
    {
        // Arrange
        // Act
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Players, Has.Member(_mockUser1));
            Assert.That(_game.Players, Has.Member(_mockUser2));
            Assert.That(_game.TurnCount, Is.EqualTo(1));
            Assert.That(_game.Players, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public async Task Test_JoinGame_ShouldNotAddPlayers_WhenGameIsStarted()
    {
        // Arrange
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        // Act
        await _game.Play(_mockUser1, "1");

        var mockUser3 = Substitute.For<IUser>();
        await _game.JoinGame(mockUser3);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Players, Does.Not.Contain(mockUser3));
            Assert.That(_game.Players, Has.Count.EqualTo(2));
            Assert.That(_game.TurnCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Test_Play_ShouldMakeMove_WhenInputIsValid()
    {
        // Arrange
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        // Act
        await _game.Play(_mockUser1, "100");

        // Assert
        Assert.That(_game.TurnCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_OnTimeout_ShouldDisqualifyPlayer_WhenTheyTimeout()
    {
        // Arrange
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        // Act
        await _game.OnTimeout();

        // Assert
        Assert.That(_game.Players, Does.Not.Contain(_mockUser1));
    }

    [Test]
    public async Task Test_Game_ShouldDeclareWinner_WhenPlayerWins()
    {
        // Arrange
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        // Act
        await _game.Play(_mockUser1, "1");
        await _game.Play(_mockUser2, "2");
        await _game.Play(_mockUser1, "1");
        await _game.Play(_mockUser2, "2");
        await _game.Play(_mockUser1, "1");
        await _game.Play(_mockUser2, "2");
        await _game.Play(_mockUser1, "1");

        // Assert
        _context.Received(1).ReplyLocalizedMessage("c4_game_win_message", _mockUser1.Name);
    }

    [Test]
    public async Task Test_Game_ShouldDeclareTie_WhenBoardIsFullWithoutWinner()
    {
        // Arrange
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        FillTieGridWithOneEmpty(_game.Grid, emptyRow: 0, emptyCol: 0);

        // Act
        await _game.Play(_mockUser1, "1");

        // Assert
        _context.Received(1).ReplyLocalizedMessage("c4_game_tie_end");
    }

    [Test]
    public async Task Test_Timeout_ShouldDisqualifyPlayer_WhenDelayElapses()
    {
        // Arrange
        var game = CreateGameWithTimeout(TimeSpan.FromMilliseconds(40));
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);

        // Act
        await Task.Delay(120);

        // Assert
        Assert.That(game.Players, Does.Not.Contain(_mockUser1));
        _context.Received(1).ReplyLocalizedMessage("c4_game_on_timeout", _mockUser1.Name);
    }

    [Test]
    public async Task Test_Timeout_ShouldBeCanceled_WhenGameIsCanceled()
    {
        // Arrange
        var game = CreateGameWithTimeout(TimeSpan.FromMilliseconds(40));
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);

        // Act
        game.Cancel();
        await Task.Delay(120);

        // Assert
        Assert.That(game.Players, Has.Member(_mockUser1));
        _context.DidNotReceive().ReplyLocalizedMessage("c4_game_on_timeout", Arg.Any<string>());
    }

    [Test]
    public async Task Test_Timeout_ShouldTargetNextPlayer_AfterMove()
    {
        // Arrange
        var game = CreateGameWithTimeout(TimeSpan.FromMilliseconds(40));
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);

        // Act
        await game.Play(_mockUser1, "1");
        await Task.Delay(120);

        // Assert
        Assert.That(game.Players, Does.Not.Contain(_mockUser2));
        _context.Received(1).ReplyLocalizedMessage("c4_game_on_timeout", _mockUser2.Name);
    }

    private ConnectFourGame CreateGameWithTimeout(TimeSpan timeoutDelay)
    {
        var ratingService = Substitute.For<IConnectFourRatingService>();
        ratingService.UpdateRatingsOnWinAsync(Arg.Any<IUser>(), Arg.Any<IUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ConnectFourRatingChange, ConnectFourRatingChange)>(
                (new ConnectFourRatingChange(1000, 1016), new ConnectFourRatingChange(1000, 984))));
        ratingService.UpdateRatingsOnDrawAsync(Arg.Any<IUser>(), Arg.Any<IUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ConnectFourRatingChange, ConnectFourRatingChange)>(
                (new ConnectFourRatingChange(1000, 1000), new ConnectFourRatingChange(1000, 1000))));
        var game = new ConnectFourGame(_mockRandomService, _mockTemplatesManager, _configuration,
            ratingService, timeoutDelay);
        game.Context = _context;
        return game;
    }

    [Test]
    public async Task Test_JoinGame_ShouldNotExceedMaxPlayers_WhenMultiplePlayersJoinConcurrently()
    {
        // Arrange
        var mockUser3 = Substitute.For<IUser>();
        mockUser3.Name.Returns("Player3");

        // Act
        await Task.WhenAll(
            _game.JoinGame(_mockUser1),
            _game.JoinGame(_mockUser2),
            _game.JoinGame(mockUser3)
        );

        // Assert
        Assert.That(_game.Players, Has.Count.EqualTo(ConnectFourConstants.MAX_PLAYERS_COUNT));
    }

    [TestCaseSource(nameof(WinningLastMoveCases))]
    public async Task Test_Game_ShouldDeclareWinner_WhenLastMoveIsWinningMove(char[,] winningGrid, string column)
    {
        // Arrange
        await _game.JoinGame(_mockUser1);
        await _game.JoinGame(_mockUser2);

        ApplyGrid(_game.Grid, winningGrid);

        // Act
        await _game.Play(_mockUser1, column);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("c4_game_win_message", _mockUser1.Name);
        _context.DidNotReceive().ReplyLocalizedMessage("c4_game_tie_end");
    }

    // Grids derived from the no-win tie pattern; '_' marks the one empty cell.
    // X wins on the last move; gravity places the piece at the topmost empty row
    // of the chosen column (rows below are all filled).
    private static IEnumerable<TestCaseData> WinningLastMoveCases()
    {
        // Horizontal: playing col 4 fills (5,3) → row 5 = X O O X X X X
        yield return new TestCaseData(
            new char[,]
            {
                { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'O' },
                { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'O' },
                { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'O', 'O', '_', 'X', 'X', 'X' }
            }, "4").SetName("Horizontal");

        // Vertical: playing col 1 fills (0,0) → col 0 rows 0-3 = X X X X
        yield return new TestCaseData(
            new char[,]
            {
                { '_', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'O' },
                { 'X', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'O' },
                { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'O' }
            }, "1").SetName("Vertical");

        // Diagonal (down-right): playing col 4 fills (0,3) → (0,3)(1,4)(2,5)(3,6) = X X X X
        yield return new TestCaseData(
            new char[,]
            {
                { 'O', 'X', 'X', '_', 'O', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'O' },
                { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'X' },
                { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'O' }
            }, "4").SetName("DiagonalDownRight");

        // Diagonal (down-left): playing col 5 fills (0,4) → (0,4)(1,3)(2,2)(3,1) = X X X X
        yield return new TestCaseData(
            new char[,]
            {
                { 'O', 'X', 'X', 'O', '_', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'O' },
                { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'X', 'O', 'X', 'X', 'O', 'O' },
                { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
                { 'X', 'O', 'O', 'X', 'X', 'O', 'O' }
            }, "5").SetName("DiagonalDownLeft");
    }

    private static void FillTieGridWithOneEmpty(char[,] grid, int emptyRow, int emptyCol)
    {
        const char empty = '_';
        var pattern = new[,]
        {
            { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
            { 'X', 'O', 'O', 'X', 'X', 'O', 'O' },
            { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
            { 'X', 'O', 'O', 'X', 'X', 'O', 'O' },
            { 'O', 'X', 'X', 'O', 'O', 'X', 'X' },
            { 'X', 'O', 'O', 'X', 'X', 'O', 'O' }
        };

        for (var row = 0; row < pattern.GetLength(0); row++)
        {
            for (var col = 0; col < pattern.GetLength(1); col++)
            {
                grid[row, col] = row == emptyRow && col == emptyCol ? empty : pattern[row, col];
            }
        }
    }

    private static void ApplyGrid(char[,] target, char[,] source)
    {
        for (var row = 0; row < source.GetLength(0); row++)
        {
            for (var col = 0; col < source.GetLength(1); col++)
            {
                target[row, col] = source[row, col];
            }
        }
    }
}
