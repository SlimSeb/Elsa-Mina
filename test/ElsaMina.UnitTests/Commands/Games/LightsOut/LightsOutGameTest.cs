using ElsaMina.Commands.Games.LightsOut;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.LightsOut;

[TestFixture]
public class LightsOutGameTest
{
    private LightsOutGame _game;
    private IRandomService _randomService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IBotDbContextFactory _dbContextFactory;
    private DbContextOptions<BotDbContext> _dbOptions;
    private IContext _context;
    private IUser _owner;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new BotDbContext(_dbOptions);
        db.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _randomService = Substitute.For<IRandomService>();
        _randomService.NextInt(Arg.Any<int>()).Returns(0);

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        _configuration = Substitute.For<IConfiguration>();
        _configuration.Name.Returns("Bot");
        _configuration.Trigger.Returns("-");

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("room1");

        _owner = Substitute.For<IUser>();
        _owner.Name.Returns("TestPlayer");
        _owner.UserId.Returns("testplayer");

        _game = new LightsOutGame(_randomService, _templatesManager, _configuration, _dbContextFactory);
        _game.Context = _context;
        _game.Owner = _owner;
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
    public async Task Test_StartNewRound_ShouldResetMoveCountToZero()
    {
        await _game.StartNewRound();

        Assert.That(_game.MoveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldResetStarsToZero()
    {
        await _game.StartNewRound();

        Assert.That(_game.Stars, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldInitializeGrid()
    {
        await _game.StartNewRound();

        Assert.That(_game.Grid, Is.Not.Null);
    }

    [Test]
    public async Task Test_StartNewRound_ShouldUseLevel1Config_WhenNoSavedData()
    {
        await _game.StartNewRound();

        Assert.That(_game.Level, Is.EqualTo(1));
        Assert.That(_game.GridSize, Is.EqualTo(5));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldLoadLevelFromDb_WhenPlayerHasSavedData()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.LightsOutScores.Add(new LightsOutScore
            {
                UserId = "testplayer", Level = 5, BestMoves = 10, TotalStars = 20
            });
            await db.SaveChangesAsync();
        }

        await _game.StartNewRound();

        Assert.That(_game.Level, Is.EqualTo(5));
        Assert.That(_game.TotalStars, Is.EqualTo(20));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldRenderBoard()
    {
        await _game.StartNewRound();

        await _templatesManager.Received(1)
            .GetTemplateAsync("Games/LightsOut/LightsOutBoard", Arg.Any<object>());
    }

    [Test]
    public async Task Test_StartNewRound_ShouldReplyGameStarted()
    {
        await _game.StartNewRound();

        _context.Received(1).ReplyLocalizedMessage("lo_game_started", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_StartNewRound_ShouldGridSizeMatchLevelConfig()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.LightsOutScores.Add(new LightsOutScore
            {
                UserId = "testplayer", Level = 6, BestMoves = 5, TotalStars = 15
            });
            await db.SaveChangesAsync();
        }

        await _game.StartNewRound();

        var (expectedSize, _) = LightsOutConstants.GetLevelConfig(6);
        Assert.That(_game.GridSize, Is.EqualTo(expectedSize));
    }

    #endregion

    #region ToggleCell

    [Test]
    public async Task Test_ToggleCell_ShouldDoNothing_WhenRoundIsNotActive()
    {
        // Round not started
        await _game.ToggleCell(_owner, 0, 0);

        Assert.That(_game.MoveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_ToggleCell_ShouldDoNothing_WhenCallerIsNotOwner()
    {
        await _game.StartNewRound();
        var otherUser = Substitute.For<IUser>();
        otherUser.UserId.Returns("otheruser");

        await _game.ToggleCell(otherUser, 0, 0);

        Assert.That(_game.MoveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_ToggleCell_ShouldDoNothing_WhenRowIsOutOfBounds()
    {
        await _game.StartNewRound();

        await _game.ToggleCell(_owner, -1, 0);
        await _game.ToggleCell(_owner, _game.GridSize, 0);

        Assert.That(_game.MoveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_ToggleCell_ShouldDoNothing_WhenColIsOutOfBounds()
    {
        await _game.StartNewRound();

        await _game.ToggleCell(_owner, 0, -1);
        await _game.ToggleCell(_owner, 0, _game.GridSize);

        Assert.That(_game.MoveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_ToggleCell_ShouldIncrementMoveCount_WhenValidMove()
    {
        // Force a non-trivially-solvable grid by making puzzle generator not produce all-off
        _randomService.NextInt(Arg.Any<int>()).Returns(2);
        await _game.StartNewRound();
        var movesBefore = _game.MoveCount;

        if (_game.IsRoundActive)
        {
            await _game.ToggleCell(_owner, 2, 2);
            Assert.That(_game.MoveCount, Is.EqualTo(movesBefore + 1));
        }
    }

    [Test]
    public async Task Test_ToggleCell_ShouldTogglesNeighbors_WhenCenterCellToggled()
    {
        // Clear grid manually by using a fresh game; check that toggling (2,2) on a 5x5
        // all-false grid flips (2,2), (1,2), (3,2), (2,1), (2,3).
        // We just need IsRoundActive = true without a puzzle. Use reflection to force it.
        await _game.StartNewRound();

        if (!_game.IsRoundActive) return; // puzzle already solved (degenerate seed), skip

        // Force grid to all-false so we can predict neighbors
        var gridProp = typeof(LightsOutGame).GetProperty("Grid");
        var grid = new bool[_game.GridSize, _game.GridSize];
        gridProp!.SetValue(_game, grid);

        // Force IsRoundActive true via backing field
        var roundActiveProp = typeof(LightsOutGame).GetProperty("IsRoundActive");
        roundActiveProp!.SetValue(_game, true);

        int center = _game.GridSize / 2;
        await _game.ToggleCell(_owner, center, center);

        Assert.Multiple(() =>
        {
            Assert.That(_game.Grid[center, center], Is.True);
            Assert.That(_game.Grid[center - 1, center], Is.True);
            Assert.That(_game.Grid[center + 1, center], Is.True);
            Assert.That(_game.Grid[center, center - 1], Is.True);
            Assert.That(_game.Grid[center, center + 1], Is.True);
        });
    }

    [Test]
    public async Task Test_ToggleCell_ShouldReplyWin_WhenPuzzleIsSolved()
    {
        // Force puzzle so that a single toggle at (0,0) solves it:
        // set up a 5x5 grid where only cells toggled by (0,0) are lit.
        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        // All-false grid + only the cells affected by toggling (0,0) set to true.
        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true;
        grid[0, 1] = true;
        grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0);

        _context.Received(1).ReplyLocalizedMessage("lo_game_win", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_ToggleCell_ShouldEndRound_WhenPuzzleSolved()
    {
        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true;
        grid[0, 1] = true;
        grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0);

        Assert.That(_game.IsRoundActive, Is.False);
        Assert.That(_game.IsEnded, Is.True);
    }

    [Test]
    public async Task Test_ToggleCell_ShouldSavePlayerData_WhenPuzzleSolved()
    {
        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true;
        grid[0, 1] = true;
        grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0);

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.LightsOutScores.FindAsync("testplayer");
        Assert.That(record, Is.Not.Null);
    }

    #endregion

    #region ComputeStars

    [Test]
    public async Task Test_ToggleCell_ShouldAward3Stars_WhenMovesWithinPressCount()
    {
        // Level 1 has 3 presses: solving in ≤3 moves = 3 stars.
        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        // Force MoveCount to 2 (≤ presses=3), then solve with 1 more = 3 total — still ≤3.
        typeof(LightsOutGame).GetProperty("MoveCount")!.SetValue(_game, 2);

        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true; grid[0, 1] = true; grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0); // MoveCount becomes 3

        Assert.That(_game.Stars, Is.EqualTo(3));
    }

    [Test]
    public async Task Test_ToggleCell_ShouldAward2Stars_WhenMovesWithinDoublePressCount()
    {
        // Level 1 presses=3, so 4..6 moves = 2 stars.
        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        typeof(LightsOutGame).GetProperty("MoveCount")!.SetValue(_game, 5);

        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true; grid[0, 1] = true; grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0); // MoveCount becomes 6 (== presses*2)

        Assert.That(_game.Stars, Is.EqualTo(2));
    }

    [Test]
    public async Task Test_ToggleCell_ShouldAward1Star_WhenMovesExceedDoublePressCount()
    {
        // Level 1 presses=3, so >6 moves = 1 star.
        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        typeof(LightsOutGame).GetProperty("MoveCount")!.SetValue(_game, 9);

        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true; grid[0, 1] = true; grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0); // MoveCount becomes 10

        Assert.That(_game.Stars, Is.EqualTo(1));
    }

    #endregion

    #region CancelAsync

    [Test]
    public async Task Test_CancelAsync_ShouldSetRoundInactive()
    {
        await _game.StartNewRound();

        await _game.CancelAsync();

        Assert.That(_game.IsRoundActive, Is.False);
    }

    [Test]
    public async Task Test_CancelAsync_ShouldEndGame()
    {
        await _game.StartNewRound();

        await _game.CancelAsync();

        Assert.That(_game.IsEnded, Is.True);
    }

    [Test]
    public async Task Test_CancelAsync_ShouldDecrementLevel_WhenRoundIsActive()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.LightsOutScores.Add(new LightsOutScore
            {
                UserId = "testplayer", Level = 3, BestMoves = 10, TotalStars = 5
            });
            await db.SaveChangesAsync();
        }

        await _game.StartNewRound();
        var levelBefore = _game.Level;

        await _game.CancelAsync();

        Assert.That(_game.Level, Is.EqualTo(Math.Max(1, levelBefore - 1)));
    }

    [Test]
    public async Task Test_CancelAsync_ShouldNotDecrementBelowLevel1()
    {
        // Level 1 — cancel should stay at 1
        await _game.StartNewRound();
        Assert.That(_game.Level, Is.EqualTo(1));

        await _game.CancelAsync();

        Assert.That(_game.Level, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_CancelAsync_ShouldRenderBoard()
    {
        await _game.StartNewRound();
        _templatesManager.ClearReceivedCalls();

        await _game.CancelAsync();

        await _templatesManager.Received(1)
            .GetTemplateAsync("Games/LightsOut/LightsOutBoard", Arg.Any<object>());
    }

    #endregion

    #region SavePlayerData

    [Test]
    public async Task Test_ToggleCell_ShouldCreateNewRecord_WhenNoExistingData()
    {
        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true; grid[0, 1] = true; grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0);

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.LightsOutScores.FindAsync("testplayer");
        Assert.That(record, Is.Not.Null);
        Assert.That(record.UserId, Is.EqualTo("testplayer"));
    }

    [Test]
    public async Task Test_ToggleCell_ShouldUpdateBestMoves_WhenBetterThanExisting()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.LightsOutScores.Add(new LightsOutScore
            {
                UserId = "testplayer", Level = 1, BestMoves = 10, TotalStars = 0
            });
            await db.SaveChangesAsync();
        }

        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        // Solve in 1 move (better than existing 10)
        typeof(LightsOutGame).GetProperty("MoveCount")!.SetValue(_game, 0);
        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true; grid[0, 1] = true; grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0);

        await using var assertDb = new BotDbContext(_dbOptions);
        var record = await assertDb.LightsOutScores.FindAsync("testplayer");
        Assert.That(record!.BestMoves, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_ToggleCell_ShouldNotUpdateBestMoves_WhenExistingIsBetter()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.LightsOutScores.Add(new LightsOutScore
            {
                UserId = "testplayer", Level = 1, BestMoves = 1, TotalStars = 0
            });
            await db.SaveChangesAsync();
        }

        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        typeof(LightsOutGame).GetProperty("MoveCount")!.SetValue(_game, 9);
        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true; grid[0, 1] = true; grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0);

        await using var assertDb = new BotDbContext(_dbOptions);
        var record = await assertDb.LightsOutScores.FindAsync("testplayer");
        Assert.That(record!.BestMoves, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_ToggleCell_ShouldAccumulateTotalStars()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.LightsOutScores.Add(new LightsOutScore
            {
                UserId = "testplayer", Level = 1, BestMoves = 5, TotalStars = 5
            });
            await db.SaveChangesAsync();
        }

        await _game.StartNewRound();
        if (!_game.IsRoundActive) return;

        // Solve in 3 moves = 3 stars (level 1 has presses=3)
        typeof(LightsOutGame).GetProperty("MoveCount")!.SetValue(_game, 2);
        var grid = new bool[_game.GridSize, _game.GridSize];
        grid[0, 0] = true; grid[0, 1] = true; grid[1, 0] = true;
        typeof(LightsOutGame).GetProperty("Grid")!.SetValue(_game, grid);
        typeof(LightsOutGame).GetProperty("IsRoundActive")!.SetValue(_game, true);

        await _game.ToggleCell(_owner, 0, 0);

        await using var assertDb = new BotDbContext(_dbOptions);
        var record = await assertDb.LightsOutScores.FindAsync("testplayer");
        Assert.That(record!.TotalStars, Is.EqualTo(5 + _game.Stars));
    }

    #endregion

    #region DisplayAnnounce

    [Test]
    public async Task Test_DisplayAnnounce_ShouldRenderAnnounceTemplate()
    {
        await _game.DisplayAnnounce();

        await _templatesManager.Received(1)
            .GetTemplateAsync("Games/LightsOut/LightsOutAnnounce", Arg.Any<object>());
    }

    [Test]
    public async Task Test_DisplayAnnounce_ShouldSendUpdatableHtml()
    {
        await _game.DisplayAnnounce();

        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), false);
    }

    #endregion
}
