using ElsaMina.Commands.Games.TwentyFortyEight;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.TwentyFortyEight;

public class TwentyFortyEightGameTest
{
    private TwentyFortyEightGame _game;
    private IRandomService _mockRandomService;
    private ITemplatesManager _mockTemplatesManager;
    private IConfiguration _mockConfiguration;
    private IDependencyContainerService _mockDependencyContainerService;
    private IContext _mockContext;
    private IUser _mockUser;
    private IBotDbContextFactory _dbContextFactory;
    private DbContextOptions<BotDbContext> _dbOptions;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _mockRandomService = Substitute.For<IRandomService>();
        _mockTemplatesManager = Substitute.For<ITemplatesManager>();
        _mockConfiguration = Substitute.For<IConfiguration>();
        _mockContext = Substitute.For<IContext>();
        _mockDependencyContainerService = Substitute.For<IDependencyContainerService>();

        DependencyContainerService.Current = _mockDependencyContainerService;

        _mockConfiguration.Name.Returns("Bot");
        _mockConfiguration.Trigger.Returns("-");
        _mockTemplatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        // NextInt(n) always returns 0: tiles spawn at first empty cell, value is always 4
        _mockRandomService.NextInt(Arg.Any<int>()).Returns(0);

        _mockUser = Substitute.For<IUser>();
        _mockUser.Name.Returns("TestPlayer");
        _mockUser.UserId.Returns("testplayer");

        _game = new TwentyFortyEightGame(_mockRandomService, _mockTemplatesManager, _mockConfiguration, _dbContextFactory);
        _game.Context = _mockContext;
        _game.Owner = _mockUser;
    }

    [TearDown]
    public void TearDown()
    {
        DependencyContainerService.Current = null;
    }

    // Sets the Grid property via reflection to enable controlled game state for testing.
    private static void SetGrid(TwentyFortyEightGame game, int[,] grid)
    {
        typeof(TwentyFortyEightGame)
            .GetProperty(nameof(TwentyFortyEightGame.Grid))!
            .SetValue(game, grid);
    }

    private static int CountNonZeroCells(int[,] grid)
    {
        var count = 0;
        for (var row = 0; row < TwentyFortyEightConstants.GRID_SIZE; row++)
            for (var col = 0; col < TwentyFortyEightConstants.GRID_SIZE; col++)
                if (grid[row, col] != 0) count++;
        return count;
    }

    #region StartNewRound

    [Test]
    public async Task Test_StartNewRound_ShouldSetRoundActive()
    {
        await _game.StartNewRound();

        Assert.That(_game.IsRoundActive, Is.True);
    }

    [Test]
    public async Task Test_StartNewRound_ShouldMarkGameAsStarted()
    {
        await _game.StartNewRound();

        Assert.That(_game.IsStarted, Is.True);
    }

    [Test]
    public async Task Test_StartNewRound_ShouldInitializeGridWithTwoTiles()
    {
        await _game.StartNewRound();

        Assert.That(_game.Grid, Is.Not.Null);
        Assert.That(CountNonZeroCells(_game.Grid), Is.EqualTo(2));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldResetScoreToZero()
    {
        await _game.StartNewRound();

        Assert.That(_game.Score, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldDefaultBestScoreAndWinsToZero_WhenNoSavedData()
    {
        await _game.StartNewRound();

        Assert.Multiple(() =>
        {
            Assert.That(_game.BestScore, Is.EqualTo(0));
            Assert.That(_game.Wins, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Test_StartNewRound_ShouldLoadBestScoreAndWinsFromDb_WhenPlayerHasSavedData()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.TwentyFortyEightScores.Add(new TwentyFortyEightScore
            {
                UserId = "testplayer",
                BestScore = 9000,
                Wins = 3
            });
            await db.SaveChangesAsync();
        }

        await _game.StartNewRound();

        Assert.Multiple(() =>
        {
            Assert.That(_game.BestScore, Is.EqualTo(9000));
            Assert.That(_game.Wins, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Test_StartNewRound_ShouldSendGameStartedMessage()
    {
        await _game.StartNewRound();

        _mockContext.Received(1).ReplyLocalizedMessage("tfe_game_started");
    }

    [Test]
    public async Task Test_StartNewRound_ShouldRenderBoard()
    {
        await _game.StartNewRound();

        await _mockTemplatesManager.Received(1)
            .GetTemplateAsync("Games/TwentyFortyEight/TwentyFortyEightBoard", Arg.Any<object>());
    }

    #endregion

    #region MakeMove

    [Test]
    public async Task Test_MakeMove_ShouldDoNothing_WhenRoundIsNotActive()
    {
        await _game.MakeMove(_mockUser, "left");

        Assert.That(_game.Grid, Is.Null);
    }

    [Test]
    public async Task Test_MakeMove_ShouldDoNothing_WhenUserIsNotOwner()
    {
        await _game.StartNewRound();
        var otherUser = Substitute.For<IUser>();
        otherUser.UserId.Returns("otherplayer");
        var initialScore = _game.Score;

        await _game.MakeMove(otherUser, "left");

        Assert.That(_game.Score, Is.EqualTo(initialScore));
    }

    [Test]
    public async Task Test_MakeMove_ShouldDoNothing_WhenDirectionIsInvalid()
    {
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4] { { 2, 4, 8, 16 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });
        _mockTemplatesManager.ClearReceivedCalls();

        await _game.MakeMove(_mockUser, "diagonal");

        await _mockTemplatesManager.DidNotReceive()
            .GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_MakeMove_ShouldDoNothing_WhenMoveDoesNotChangeBoard()
    {
        await _game.StartNewRound();
        // Already left-aligned, no adjacent equal - left slide does nothing
        SetGrid(_game, new int[4, 4] { { 2, 4, 8, 16 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });
        _mockTemplatesManager.ClearReceivedCalls();

        await _game.MakeMove(_mockUser, "left");

        await _mockTemplatesManager.DidNotReceive()
            .GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_MakeMove_ShouldSpawnNewTile_AfterValidMove()
    {
        await _game.StartNewRound();
        // One tile not at the leftmost position - slide left moves it
        SetGrid(_game, new int[4, 4] { { 0, 2, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        await _game.MakeMove(_mockUser, "left");

        Assert.That(CountNonZeroCells(_game.Grid), Is.EqualTo(2));
    }

    [Test]
    public async Task Test_MakeMove_ShouldIncreaseScore_WhenTilesMerge()
    {
        await _game.StartNewRound();
        // Two equal tiles side by side - sliding left merges them
        SetGrid(_game, new int[4, 4] { { 4, 4, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        await _game.MakeMove(_mockUser, "left");

        Assert.That(_game.Score, Is.EqualTo(8));
    }

    [Test]
    public async Task Test_MakeMove_ShouldWin_WhenReaching2048Tile()
    {
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4] { { 1024, 1024, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        await _game.MakeMove(_mockUser, "left");

        Assert.Multiple(() =>
        {
            Assert.That(_game.IsRoundActive, Is.False);
            Assert.That(_game.IsEnded, Is.True);
        });
    }

    [Test]
    public async Task Test_MakeMove_ShouldSendWinMessage_WhenReaching2048Tile()
    {
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4] { { 1024, 1024, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        await _game.MakeMove(_mockUser, "left");

        _mockContext.Received(1).ReplyLocalizedMessage("tfe_game_win",
            Arg.Any<object>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_MakeMove_ShouldIncrementWins_WhenReaching2048Tile()
    {
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4] { { 1024, 1024, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        await _game.MakeMove(_mockUser, "left");

        Assert.That(_game.Wins, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_MakeMove_ShouldUpdateBestScore_WhenScoreExceedsPreviousBest()
    {
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4] { { 1024, 1024, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        await _game.MakeMove(_mockUser, "left");

        Assert.That(_game.BestScore, Is.EqualTo(2048));
    }

    [Test]
    public async Task Test_MakeMove_ShouldSaveToDb_WhenWin()
    {
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4] { { 1024, 1024, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        await _game.MakeMove(_mockUser, "left");

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.TwentyFortyEightScores.FindAsync("testplayer");
        Assert.That(record, Is.Not.Null);
        Assert.That(record.Wins, Is.EqualTo(1));
        Assert.That(record.BestScore, Is.EqualTo(2048));
    }

    [Test]
    public async Task Test_MakeMove_ShouldLose_WhenNoMovesAvailable()
    {
        // Pre-move grid: full board minus [3,3]. Slide right on row 3 moves tiles left,
        // then spawning 4 at [3,0] produces a board with no empty cells and no adjacent equal tiles.
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4]
        {
            {  64, 128,  4, 32 },
            {  16,  32,  8, 256 },
            {   2, 128, 64, 512 },
            {   8,  16, 32,   0 }
        });

        await _game.MakeMove(_mockUser, "right");

        Assert.Multiple(() =>
        {
            Assert.That(_game.IsRoundActive, Is.False);
            Assert.That(_game.IsEnded, Is.True);
        });
    }

    [Test]
    public async Task Test_MakeMove_ShouldSendLoseMessage_WhenNoMovesAvailable()
    {
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4]
        {
            {  64, 128,  4, 32 },
            {  16,  32,  8, 256 },
            {   2, 128, 64, 512 },
            {   8,  16, 32,   0 }
        });

        await _game.MakeMove(_mockUser, "right");

        _mockContext.Received(1).ReplyLocalizedMessage("tfe_game_lose",
            Arg.Any<object>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_MakeMove_ShouldSaveToDb_WhenLose()
    {
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4]
        {
            {  64, 128,  4, 32 },
            {  16,  32,  8, 256 },
            {   2, 128, 64, 512 },
            {   8,  16, 32,   0 }
        });

        await _game.MakeMove(_mockUser, "right");

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.TwentyFortyEightScores.FindAsync("testplayer");
        Assert.That(record, Is.Not.Null);
    }

    [Test]
    public async Task Test_MakeMove_ShouldRenderBoard_AfterValidMove()
    {
        await _game.StartNewRound();
        SetGrid(_game, new int[4, 4] { { 0, 2, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });
        _mockTemplatesManager.ClearReceivedCalls();

        await _game.MakeMove(_mockUser, "left");

        await _mockTemplatesManager.Received(1)
            .GetTemplateAsync("Games/TwentyFortyEight/TwentyFortyEightBoard", Arg.Any<object>());
    }

    #endregion

    #region CancelAsync

    [Test]
    public async Task Test_CancelAsync_ShouldEndGame()
    {
        await _game.StartNewRound();

        await _game.CancelAsync();

        Assert.That(_game.IsEnded, Is.True);
    }

    [Test]
    public async Task Test_CancelAsync_ShouldDeactivateRound()
    {
        await _game.StartNewRound();

        await _game.CancelAsync();

        Assert.That(_game.IsRoundActive, Is.False);
    }

    [Test]
    public async Task Test_CancelAsync_ShouldNotSaveToDb()
    {
        await _game.StartNewRound();

        await _game.CancelAsync();

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.TwentyFortyEightScores.FindAsync("testplayer");
        Assert.That(record, Is.Null);
    }

    [Test]
    public async Task Test_CancelAsync_ShouldWork_WhenNoRoundActive()
    {
        Assert.DoesNotThrowAsync(async () => await _game.CancelAsync());
    }

    [Test]
    public async Task Test_CancelAsync_ShouldRenderBoard()
    {
        await _game.StartNewRound();
        _mockTemplatesManager.ClearReceivedCalls();

        await _game.CancelAsync();

        await _mockTemplatesManager.Received(1)
            .GetTemplateAsync("Games/TwentyFortyEight/TwentyFortyEightBoard", Arg.Any<object>());
    }

    #endregion
}
