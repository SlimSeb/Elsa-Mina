using ElsaMina.Commands.Games.FloodIt;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.FloodIt;

public class FloodItGameTest
{
    private FloodItGame _game;
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

        // Default: all cells return color 0 → entire grid is one color, fully claimed on init
        _mockRandomService.NextInt(Arg.Any<int>()).Returns(0);

        _mockUser = Substitute.For<IUser>();
        _mockUser.Name.Returns("TestPlayer");
        _mockUser.UserId.Returns("testplayer");

        _game = new FloodItGame(_mockRandomService, _mockTemplatesManager, _mockConfiguration, _dbContextFactory);
        _game.Context = _mockContext;
        _game.Owner = _mockUser;
    }

    [TearDown]
    public void TearDown()
    {
        DependencyContainerService.Current = null;
    }

    // Helper: produce a two-color grid (color 0 at [0,0], color 1 everywhere else).
    // The greedy solver will pick color 1 in 1 move, which is <= MaxMoves, so init won't loop.
    private void SetUpTwoColorGrid()
    {
        var callCount = 0;
        _mockRandomService.NextInt(Arg.Any<int>()).Returns(_ =>
        {
            callCount++;
            return callCount == 1 ? 0 : 1;
        });
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
    public async Task Test_StartNewRound_ShouldInitializeGridAndClaimedArrays()
    {
        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Grid, Is.Not.Null);
            Assert.That(_game.IsClaimed, Is.Not.Null);
        }
    }

    [Test]
    public async Task Test_StartNewRound_ShouldSetMoveCountToZero()
    {
        await _game.StartNewRound();

        Assert.That(_game.MoveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldRenderBoard()
    {
        await _game.StartNewRound();

        await _mockTemplatesManager.Received(1)
            .GetTemplateAsync("Games/FloodIt/FloodItBoard", Arg.Any<object>());
    }

    [Test]
    public async Task Test_StartNewRound_ShouldDefaultToLevel1_WhenNoSavedData()
    {
        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Level, Is.EqualTo(1));
            Assert.That(_game.TotalStars, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Test_StartNewRound_ShouldLoadLevelFromDb_WhenPlayerHasSavedData()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.FloodItScores.Add(new DataAccess.Models.FloodItScore
            {
                UserId = "testplayer",
                Level = 4,
                TotalStars = 7,
                BestMoves = 20
            });
            await db.SaveChangesAsync();
        }

        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Level, Is.EqualTo(4));
            Assert.That(_game.TotalStars, Is.EqualTo(7));
        }
    }

    [Test]
    public async Task Test_StartNewRound_ShouldSendGameStartedMessage()
    {
        await _game.StartNewRound();

        _mockContext.Received(1).ReplyLocalizedMessage("fi_game_started",
            Arg.Any<object>(), Arg.Any<object>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_StartNewRound_ShouldSetGridSizeFromLevelConfig()
    {
        await _game.StartNewRound();

        // Level 1 config: GridSize = 8
        Assert.That(_game.GridSize, Is.EqualTo(8));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldSetMaxMovesFromLevelConfig()
    {
        await _game.StartNewRound();

        Assert.That(_game.MaxMoves, Is.EqualTo(FloodItConstants.GetLevelConfig(1).MaxMoves));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldClaimTopLeftCell()
    {
        await _game.StartNewRound();

        Assert.That(_game.IsClaimed[0, 0], Is.True);
    }

    #endregion

    #region FloodFill

    [Test]
    public async Task Test_FloodFill_ShouldDoNothing_WhenRoundIsNotActive()
    {
        await _game.FloodFill(_mockUser, 1);

        Assert.That(_game.Grid, Is.Null);
    }

    [Test]
    public async Task Test_FloodFill_ShouldDoNothing_WhenUserIsNotOwner()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();
        var otherUser = Substitute.For<IUser>();
        otherUser.UserId.Returns("otherplayer");

        await _game.FloodFill(otherUser, 1);

        Assert.That(_game.MoveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_FloodFill_ShouldDoNothing_WhenColorIndexIsOutOfRange()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.FloodFill(_mockUser, -1);
        await _game.FloodFill(_mockUser, _game.ColorCount);

        Assert.That(_game.MoveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_FloodFill_ShouldDoNothing_WhenColorMatchesCurrentClaimedColor()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();
        var currentColor = _game.Grid[0, 0];

        await _game.FloodFill(_mockUser, currentColor);

        Assert.That(_game.MoveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_FloodFill_ShouldIncrementMoveCount_WhenValidMoveApplied()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.FloodFill(_mockUser, 1);

        Assert.That(_game.MoveCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_FloodFill_ShouldWinGame_WhenAllCellsClaimed()
    {
        // All-zero grid: [0,0] is color 0, entire grid is already claimed after init.
        // FloodFill won't be triggered because no color differs. Instead, use two-color grid and flood to 1.
        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.FloodFill(_mockUser, 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsRoundActive, Is.False);
            Assert.That(_game.IsEnded, Is.True);
        }
    }

    [Test]
    public async Task Test_FloodFill_ShouldSendWinMessage_WhenAllCellsClaimed()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.FloodFill(_mockUser, 1);

        _mockContext.Received(1).ReplyLocalizedMessage("fi_game_win",
            Arg.Any<object>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_FloodFill_ShouldAdvanceLevel_WhenWin()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.FloodFill(_mockUser, 1);

        Assert.That(_game.Level, Is.EqualTo(2));
    }

    [Test]
    public async Task Test_FloodFill_ShouldNotExceedMaxLevel_WhenWinningAtMaxLevel()
    {
        // Seed level at max in DB
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.FloodItScores.Add(new DataAccess.Models.FloodItScore
            {
                UserId = "testplayer",
                Level = FloodItConstants.MAX_LEVEL,
                TotalStars = 0,
                BestMoves = 0
            });
            await db.SaveChangesAsync();
        }

        SetUpTwoColorGrid();
        await _game.StartNewRound();
        await _game.FloodFill(_mockUser, 1);

        Assert.That(_game.Level, Is.EqualTo(FloodItConstants.MAX_LEVEL));
    }

    [Test]
    public async Task Test_FloodFill_ShouldLoseGame_WhenMaxMovesExceeded()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();
        var maxMoves = _game.MaxMoves;

        // Exhaust all moves without winning: alternate colors 2 and 3 (not in the grid → no gain)
        for (var move = 0; move < maxMoves; move++)
        {
            var wasteColor = (move % 2 == 0) ? 2 : 3;
            if (wasteColor < _game.ColorCount)
            {
                await _game.FloodFill(_mockUser, wasteColor);
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsRoundActive, Is.False);
            Assert.That(_game.IsEnded, Is.True);
        }
    }

    [Test]
    public async Task Test_FloodFill_ShouldSendLoseMessage_WhenMaxMovesExceeded()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();
        var maxMoves = _game.MaxMoves;

        for (var move = 0; move < maxMoves; move++)
        {
            var wasteColor = (move % 2 == 0) ? 2 : 3;
            if (wasteColor < _game.ColorCount)
            {
                await _game.FloodFill(_mockUser, wasteColor);
            }
        }

        _mockContext.Received(1).ReplyLocalizedMessage("fi_game_lose",
            Arg.Any<object>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_FloodFill_ShouldDropLevel_WhenLose()
    {
        // Seed level 3 so drop is visible
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.FloodItScores.Add(new DataAccess.Models.FloodItScore
            {
                UserId = "testplayer",
                Level = 3,
                TotalStars = 0,
                BestMoves = 0
            });
            await db.SaveChangesAsync();
        }

        SetUpTwoColorGrid();
        await _game.StartNewRound();
        var maxMoves = _game.MaxMoves;

        for (var move = 0; move < maxMoves; move++)
        {
            var wasteColor = (move % 2 == 0) ? 2 : 3;
            if (wasteColor < _game.ColorCount)
            {
                await _game.FloodFill(_mockUser, wasteColor);
            }
        }

        Assert.That(_game.Level, Is.EqualTo(2));
    }

    [Test]
    public async Task Test_FloodFill_ShouldNotDropBelowLevel1_WhenLoseAtLevel1()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();
        var maxMoves = _game.MaxMoves;

        for (var move = 0; move < maxMoves; move++)
        {
            var wasteColor = (move % 2 == 0) ? 2 : 3;
            if (wasteColor < _game.ColorCount)
            {
                await _game.FloodFill(_mockUser, wasteColor);
            }
        }

        Assert.That(_game.Level, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Test_FloodFill_ShouldAward3Stars_WhenWonWithinTargetMoves()
    {
        // Level 1: TargetMoves = 13. Win in 1 move (two-color grid) → 3 stars
        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.FloodFill(_mockUser, 1);

        Assert.That(_game.Stars, Is.EqualTo(3));
    }

    [Test]
    public async Task Test_FloodFill_ShouldSaveDataToDb_WhenWin()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.FloodFill(_mockUser, 1);

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.FloodItScores.FindAsync("testplayer");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record, Is.Not.Null);
            Assert.That(record.Level, Is.EqualTo(2));
            Assert.That(record.TotalStars, Is.GreaterThan(0));
        }
    }

    [Test]
    public async Task Test_FloodFill_ShouldSaveLevelToDb_WhenLose()
    {
        await using (var seedDb = new BotDbContext(_dbOptions))
        {
            seedDb.FloodItScores.Add(new DataAccess.Models.FloodItScore
            {
                UserId = "testplayer",
                Level = 2,
                TotalStars = 0,
                BestMoves = 0
            });
            await seedDb.SaveChangesAsync();
        }

        SetUpTwoColorGrid();
        await _game.StartNewRound();
        var maxMoves = _game.MaxMoves;

        for (var move = 0; move < maxMoves; move++)
        {
            var wasteColor = (move % 2 == 0) ? 2 : 3;
            if (wasteColor < _game.ColorCount)
            {
                await _game.FloodFill(_mockUser, wasteColor);
            }
        }

        await using var verifyDb = new BotDbContext(_dbOptions);
        var record = await verifyDb.FloodItScores.FindAsync("testplayer");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record, Is.Not.Null);
            Assert.That(record.Level, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_FloodFill_ShouldRenderBoard_AfterEachMove()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();
        _mockTemplatesManager.ClearReceivedCalls();

        await _game.FloodFill(_mockUser, 1);

        await _mockTemplatesManager.Received(1)
            .GetTemplateAsync("Games/FloodIt/FloodItBoard", Arg.Any<object>());
    }

    #endregion

    #region CancelAsync

    [Test]
    public async Task Test_CancelAsync_ShouldEndGame()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.CancelAsync();

        Assert.That(_game.IsEnded, Is.True);
    }

    [Test]
    public async Task Test_CancelAsync_ShouldDeactivateRound()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.CancelAsync();

        Assert.That(_game.IsRoundActive, Is.False);
    }

    [Test]
    public async Task Test_CancelAsync_ShouldDropLevel_WhenRoundWasActive()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.FloodItScores.Add(new DataAccess.Models.FloodItScore
            {
                UserId = "testplayer",
                Level = 3,
                TotalStars = 0,
                BestMoves = 0
            });
            await db.SaveChangesAsync();
        }

        SetUpTwoColorGrid();
        await _game.StartNewRound();

        await _game.CancelAsync();

        Assert.That(_game.Level, Is.EqualTo(2));
    }

    [Test]
    public async Task Test_CancelAsync_ShouldNotDropBelowLevel1()
    {
        await _game.StartNewRound();

        await _game.CancelAsync();

        Assert.That(_game.Level, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Test_CancelAsync_ShouldSaveLevelToDb_WhenRoundWasActive()
    {
        await using (var seedDb = new BotDbContext(_dbOptions))
        {
            seedDb.FloodItScores.Add(new DataAccess.Models.FloodItScore
            {
                UserId = "testplayer",
                Level = 2,
                TotalStars = 0,
                BestMoves = 0
            });
            await seedDb.SaveChangesAsync();
        }

        SetUpTwoColorGrid();
        await _game.StartNewRound();
        await _game.CancelAsync();

        await using var verifyDb = new BotDbContext(_dbOptions);
        var record = await verifyDb.FloodItScores.FindAsync("testplayer");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record, Is.Not.Null);
            Assert.That(record.Level, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_CancelAsync_ShouldWork_WhenNoRoundActive()
    {
        Assert.DoesNotThrowAsync(async () => await _game.CancelAsync());
    }

    [Test]
    public async Task Test_CancelAsync_ShouldRenderBoard()
    {
        SetUpTwoColorGrid();
        await _game.StartNewRound();
        _mockTemplatesManager.ClearReceivedCalls();

        await _game.CancelAsync();

        await _mockTemplatesManager.Received(1)
            .GetTemplateAsync("Games/FloodIt/FloodItBoard", Arg.Any<object>());
    }

    #endregion
}
