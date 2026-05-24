using ElsaMina.Commands.Games.VoltorbFlip;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.VoltorbFlip;

public class VoltorbFlipGameTest
{
    private VoltorbFlipGame _game;
    private IRandomService _mockRandomService;
    private ITemplatesManager _mockTemplatesManager;
    private IConfiguration _mockConfiguration;
    private IDependencyContainerService _mockDependencyContainerService;
    private IContext _mockContext;
    private IUser _mockUser;
    private IBotDbContextFactory _dbContextFactory;
    private DbContextOptions<BotDbContext> _dbOptions;

    // Config: 1 voltorb, 1 two, 0 threes - with ShuffleInPlace doing nothing the layout is:
    // [0,0]=Voltorb  [0,1]=2  [0,2]=1  [0,3]=1  [0,4]=1
    // [1..4, 0..4] = all 1s
    private static readonly (int Twos, int Threes, int Voltorbs) TestConfig = (1, 0, 1);

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
        _mockRandomService
            .RandomElement(Arg.Any<IEnumerable<(int Twos, int Threes, int Voltorbs)>>())
            .Returns(TestConfig);

        _mockUser = Substitute.For<IUser>();
        _mockUser.Name.Returns("TestPlayer");
        _mockUser.UserId.Returns("testplayer");

        _game = new VoltorbFlipGame(_mockRandomService, _mockTemplatesManager, _mockConfiguration, _dbContextFactory);
        _game.Context = _mockContext;
        _game.Owner = _mockUser;
    }

    [TearDown]
    public void TearDown()
    {
        DependencyContainerService.Current = null;
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
    public async Task Test_StartNewRound_ShouldStartNewSession_WhenCalledAfterRoundEnds()
    {
        await _game.StartNewRound();
        await _game.QuitRound(_mockUser); // game ends

        await _game.StartNewRound(); // starts a new session

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsStarted, Is.True);
            Assert.That(_game.IsEnded, Is.False);
        }
    }

    [Test]
    public async Task Test_StartNewRound_ShouldResetCoinsToZero()
    {
        await _game.StartNewRound();

        Assert.That(_game.CurrentCoins, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldInitializeBoardArrays()
    {
        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.TileValues, Is.Not.Null);
            Assert.That(_game.IsRevealed, Is.Not.Null);
            Assert.That(_game.RowSums, Is.Not.Null);
            Assert.That(_game.ColSums, Is.Not.Null);
            Assert.That(_game.RowVoltorbs, Is.Not.Null);
            Assert.That(_game.ColVoltorbs, Is.Not.Null);
        }
    }

    [Test]
    public async Task Test_StartNewRound_ShouldRenderBoard()
    {
        await _game.StartNewRound();

        await _mockTemplatesManager.Received(1)
            .GetTemplateAsync("Games/VoltorbFlip/VoltorbFlipBoard", Arg.Any<object>());
    }

    [Test]
    public async Task Test_StartNewRound_ShouldResetRevealedTiles_WhenStartingSecondRound()
    {
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 2); // Reveal a tile
        await _game.QuitRound(_mockUser);

        await _game.StartNewRound();

        Assert.That(_game.IsRevealed[0, 2], Is.False);
    }

    [Test]
    public async Task Test_StartNewRound_ShouldLoadLevelFromDb_WhenPlayerHasSavedData()
    {
        // Arrange - seed saved level 5
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.VoltorbFlipLevels.Add(new DataAccess.Models.VoltorbFlipLevel
            {
                UserId = "testplayer",
                Level = 5,
                Coins = 100
            });
            await db.SaveChangesAsync();
        }

        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Level, Is.EqualTo(5));
            Assert.That(_game.TotalCoins, Is.EqualTo(100));
        }
    }

    [Test]
    public async Task Test_StartNewRound_ShouldDefaultToLevel1_WhenNoSavedData()
    {
        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Level, Is.EqualTo(1));
            Assert.That(_game.TotalCoins, Is.EqualTo(0));
        }
    }

    #endregion

    #region Row/Column statistics

    [Test]
    public async Task Test_StartNewRound_ShouldComputeCorrectRowSums()
    {
        // Row 0: Voltorb(0) + 2 + 1 + 1 + 1 = 5; rows 1-4: 5 ones = 5
        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.RowSums[0], Is.EqualTo(5));
            Assert.That(_game.RowSums[1], Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Test_StartNewRound_ShouldComputeCorrectRowVoltorbs()
    {
        // Row 0 has the Voltorb at [0,0]; all other rows have none
        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.RowVoltorbs[0], Is.EqualTo(1));
            Assert.That(_game.RowVoltorbs[1], Is.EqualTo(0));
            Assert.That(_game.RowVoltorbs[2], Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Test_StartNewRound_ShouldComputeCorrectColSums()
    {
        // Col 0: Voltorb + 4 ones = 4; col 1: 2 + 4 ones = 6; cols 2-4: 5 ones = 5
        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.ColSums[0], Is.EqualTo(4));
            Assert.That(_game.ColSums[1], Is.EqualTo(6));
            Assert.That(_game.ColSums[2], Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Test_StartNewRound_ShouldComputeCorrectColVoltorbs()
    {
        // Voltorb is at [0,0] so only col 0 has a Voltorb
        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.ColVoltorbs[0], Is.EqualTo(1));
            Assert.That(_game.ColVoltorbs[1], Is.EqualTo(0));
        }
    }

    #endregion

    #region FlipTile

    [Test]
    public async Task Test_FlipTile_ShouldDoNothing_WhenRoundIsNotActive()
    {
        await _game.FlipTile(_mockUser, 0, 0);

        Assert.That(_game.IsRevealed, Is.Null);
    }

    [Test]
    public async Task Test_FlipTile_ShouldDoNothing_WhenUserIsNotOwner()
    {
        await _game.StartNewRound();
        var otherUser = Substitute.For<IUser>();
        otherUser.UserId.Returns("otherplayer");

        await _game.FlipTile(otherUser, 0, 2);

        Assert.That(_game.IsRevealed[0, 2], Is.False);
    }

    [Test]
    public async Task Test_FlipTile_ShouldDoNothing_WhenRowIsOutOfBounds()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, -1, 0);
        await _game.FlipTile(_mockUser, VoltorbFlipConstants.GRID_SIZE, 0);

        Assert.That(_game.CurrentCoins, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_FlipTile_ShouldDoNothing_WhenColIsOutOfBounds()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, -1);
        await _game.FlipTile(_mockUser, 0, VoltorbFlipConstants.GRID_SIZE);

        Assert.That(_game.CurrentCoins, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_FlipTile_ShouldDoNothing_WhenTileAlreadyRevealed()
    {
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 2); // 1x tile → coins = 1

        await _game.FlipTile(_mockUser, 0, 2); // flip again

        // 2 calls expected: 1 from StartNewRound + 1 from first FlipTile; second flip should not add another
        _mockContext.Received(2).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_FlipTile_ShouldRevealTile_WhenFlipped()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 2);

        Assert.That(_game.IsRevealed[0, 2], Is.True);
    }

    [Test]
    public async Task Test_FlipTile_ShouldEndGame_WhenVoltorbFlipped()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 0); // Voltorb at [0,0]

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsRoundActive, Is.False);
            Assert.That(_game.IsEnded, Is.True);
        }
    }

    [Test]
    public async Task Test_FlipTile_ShouldSendVoltorbMessage_WhenVoltorbFlipped()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 0);

        _mockContext.Received(1).ReplyLocalizedMessage("vf_game_voltorb_hit", _mockUser.Name, Arg.Any<int>());
    }

    [Test]
    public async Task Test_FlipTile_ShouldKeepLevel_WhenVoltorbFlippedWithEnoughRevealedCards()
    {
        // At level 1: reveal 1 non-Voltorb tile first (revealed=1 >= level=1) → keep level
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 2); // 1x tile
        await _game.FlipTile(_mockUser, 0, 0); // Voltorb

        Assert.That(_game.Level, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_FlipTile_ShouldDropToMinLevel_WhenVoltorbFlippedWithNoRevealedCards()
    {
        // At level 1: flip Voltorb immediately → revealed=0 < level=1 → max(1, 0) = 1
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 0); // Voltorb immediately

        Assert.That(_game.Level, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_FlipTile_ShouldDropLevel_WhenVoltorbFlippedWithInsufficientRevealedCards()
    {
        // Win once (level → 2), then flip Voltorb on new round with 0 revealed → drop to max(1,0)=1
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 1); // Win → level 2
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 0); // Voltorb immediately, 0 < 2 → level 1

        Assert.That(_game.Level, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_FlipTile_ShouldSetCoinsToTileValue_WhenFirstTileFlipped()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 1); // 2x tile → coins = 2

        // Won the round (only 2x tile), check the win message carries correct coins
        _mockContext.Received(1).ReplyLocalizedMessage("vf_game_win", _mockUser.Name, 2, Arg.Any<int>());
    }

    [Test]
    public async Task Test_FlipTile_ShouldSetCoinsToOne_WhenFirstOneTileFlipped()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 2); // 1x tile

        Assert.That(_game.CurrentCoins, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_FlipTile_ShouldMultiplyCoins_WhenMultipleTilesFlipped()
    {
        // Flip 1x then 2x (but 2x wins the round): coins = 2^1 * 3^0 = 2
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 2); // 1x → coins = 1
        await _game.FlipTile(_mockUser, 0, 1); // 2x → win, coins = 2

        _mockContext.Received(1).ReplyLocalizedMessage("vf_game_win", _mockUser.Name, 2, Arg.Any<int>());
    }

    [Test]
    public async Task Test_FlipTile_ShouldEndGame_WhenAllMultiplierTilesRevealed()
    {
        // Config has 1 two, 0 threes → flipping the single 2x tile wins
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 1); // Only 2x tile

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsRoundActive, Is.False);
            Assert.That(_game.IsEnded, Is.True);
        }
    }

    [Test]
    public async Task Test_FlipTile_ShouldSendWinMessage_WhenRoundWon()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 1);

        _mockContext.Received(1).ReplyLocalizedMessage("vf_game_win", _mockUser.Name, Arg.Any<int>(), Arg.Any<int>());
    }

    [Test]
    public async Task Test_FlipTile_ShouldAdvanceLevel_WhenRoundWon()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 1); // Win

        Assert.That(_game.Level, Is.EqualTo(2));
    }

    [Test]
    public async Task Test_FlipTile_ShouldNotExceedMaxLevel_WhenWinningAtMaxLevel()
    {
        for (var i = 0; i < VoltorbFlipConstants.MAX_LEVEL; i++)
        {
            await _game.StartNewRound();
            await _game.FlipTile(_mockUser, 0, 1); // Win each round
        }

        Assert.That(_game.Level, Is.EqualTo(VoltorbFlipConstants.MAX_LEVEL));
    }

    [Test]
    public async Task Test_FlipTile_ShouldSaveLevelToDb_WhenVoltorbFlipped()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 0); // Voltorb

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.VoltorbFlipLevels.FindAsync("testplayer");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record, Is.Not.Null);
            Assert.That(record.Level, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_FlipTile_ShouldSaveLevelAndCoinsToDb_WhenRoundWon()
    {
        await _game.StartNewRound();

        await _game.FlipTile(_mockUser, 0, 1); // Win with 2x tile → coins = 2, level → 2

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.VoltorbFlipLevels.FindAsync("testplayer");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record, Is.Not.Null);
            Assert.That(record.Level, Is.EqualTo(2));
            Assert.That(record.MaxLevel, Is.EqualTo(2));
            Assert.That(record.Coins, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Test_FlipTile_ShouldNotDecreaseMaxLevel_WhenVoltorbHitAfterHigherLevelReached()
    {
        // Win once: level goes 1 → 2, MaxLevel = 2
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 1);

        // Lose immediately: level drops back to 1, MaxLevel should stay 2
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 0); // Voltorb

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.VoltorbFlipLevels.FindAsync("testplayer");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Level, Is.EqualTo(1));
            Assert.That(record.MaxLevel, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Test_FlipTile_ShouldUpdateMaxLevel_WhenNewHighLevelReached()
    {
        // Win round 1: level 1 → 2
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 1);

        // Win round 2: level 2 → 3
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 1);

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.VoltorbFlipLevels.FindAsync("testplayer");
        Assert.That(record.MaxLevel, Is.EqualTo(3));
    }

    #endregion

    #region SetMarkerType

    [Test]
    public async Task Test_SetMarkerType_ShouldHaveNoActiveMarker_BeforeRoundStarts()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.ActiveMarkerType, Is.EqualTo(VoltorbFlipMarkerType.None));
            Assert.That(_game.IsMarkingMode, Is.False);
        }
    }

    [Test]
    public async Task Test_SetMarkerType_ShouldEnableMarkingMode_WhenCalledWithAMarkerType()
    {
        await _game.StartNewRound();

        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsMarkingMode, Is.True);
            Assert.That(_game.ActiveMarkerType, Is.EqualTo(VoltorbFlipMarkerType.Voltorb));
        }
    }

    [Test]
    public async Task Test_SetMarkerType_ShouldDisableMarkingMode_WhenCalledTwiceWithSameType()
    {
        await _game.StartNewRound();

        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb);
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsMarkingMode, Is.False);
            Assert.That(_game.ActiveMarkerType, Is.EqualTo(VoltorbFlipMarkerType.None));
        }
    }

    [Test]
    public async Task Test_SetMarkerType_ShouldSwitchMarkerType_WhenCalledWithDifferentType()
    {
        await _game.StartNewRound();

        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb);
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Two);

        Assert.That(_game.ActiveMarkerType, Is.EqualTo(VoltorbFlipMarkerType.Two));
    }

    [Test]
    public async Task Test_SetMarkerType_ShouldDoNothing_WhenUserIsNotOwner()
    {
        await _game.StartNewRound();
        var otherUser = Substitute.For<IUser>();
        otherUser.UserId.Returns("otherplayer");

        await _game.SetMarkerType(otherUser, VoltorbFlipMarkerType.Voltorb);

        Assert.That(_game.IsMarkingMode, Is.False);
    }

    [Test]
    public async Task Test_SetMarkerType_ShouldDoNothing_WhenRoundNotActive()
    {
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb);

        Assert.That(_game.IsMarkingMode, Is.False);
    }

    [Test]
    public async Task Test_SetMarkerType_ShouldSetActiveMarkerTypeToOne_WhenCalledWithOne()
    {
        await _game.StartNewRound();

        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.One);

        Assert.That(_game.ActiveMarkerType, Is.EqualTo(VoltorbFlipMarkerType.One));
    }

    [Test]
    public async Task Test_SetMarkerType_ShouldSetActiveMarkerTypeToThree_WhenCalledWithThree()
    {
        await _game.StartNewRound();

        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Three);

        Assert.That(_game.ActiveMarkerType, Is.EqualTo(VoltorbFlipMarkerType.Three));
    }

    [Test]
    public async Task Test_FlipTile_ShouldMarkCell_WhenInMarkingMode()
    {
        await _game.StartNewRound();
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb);

        await _game.FlipTile(_mockUser, 0, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Markers[0, 2], Is.EqualTo(VoltorbFlipMarkerType.Voltorb));
            Assert.That(_game.IsRevealed[0, 2], Is.False);
        }
    }

    [Test]
    public async Task Test_FlipTile_ShouldMarkCellWithOneMarker_WhenActiveMarkerIsOne()
    {
        await _game.StartNewRound();
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.One);

        await _game.FlipTile(_mockUser, 0, 2);

        Assert.That(_game.Markers[0, 2], Is.EqualTo(VoltorbFlipMarkerType.One));
    }

    [Test]
    public async Task Test_FlipTile_ShouldMarkCellWithTwoMarker_WhenActiveMarkerIsTwo()
    {
        await _game.StartNewRound();
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Two);

        await _game.FlipTile(_mockUser, 0, 2);

        Assert.That(_game.Markers[0, 2], Is.EqualTo(VoltorbFlipMarkerType.Two));
    }

    [Test]
    public async Task Test_FlipTile_ShouldMarkCellWithThreeMarker_WhenActiveMarkerIsThree()
    {
        await _game.StartNewRound();
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Three);

        await _game.FlipTile(_mockUser, 0, 2);

        Assert.That(_game.Markers[0, 2], Is.EqualTo(VoltorbFlipMarkerType.Three));
    }

    [Test]
    public async Task Test_FlipTile_ShouldUnmarkCell_WhenInMarkingModeAndCellAlreadyMarkedWithSameType()
    {
        await _game.StartNewRound();
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb);
        await _game.FlipTile(_mockUser, 0, 2); // mark

        await _game.FlipTile(_mockUser, 0, 2); // unmark

        Assert.That(_game.Markers[0, 2], Is.EqualTo(VoltorbFlipMarkerType.None));
    }

    [Test]
    public async Task Test_FlipTile_ShouldReplaceMarker_WhenInMarkingModeAndCellMarkedWithDifferentType()
    {
        await _game.StartNewRound();
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb);
        await _game.FlipTile(_mockUser, 0, 2); // mark as Voltorb
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb); // deactivate voltorb mode
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Two); // activate Two mode

        await _game.FlipTile(_mockUser, 0, 2); // replace marker

        Assert.That(_game.Markers[0, 2], Is.EqualTo(VoltorbFlipMarkerType.Two));
    }

    [Test]
    public async Task Test_FlipTile_ShouldNotFlipCell_WhenInMarkingMode()
    {
        await _game.StartNewRound();
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.One);

        await _game.FlipTile(_mockUser, 0, 2);

        Assert.That(_game.CurrentCoins, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldResetMarkingModeAndMarkers_WhenStartingNewRound()
    {
        await _game.StartNewRound();
        await _game.SetMarkerType(_mockUser, VoltorbFlipMarkerType.Voltorb);
        await _game.FlipTile(_mockUser, 0, 2); // mark a cell
        _game.Owner = _mockUser;

        await _game.StartNewRound();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsMarkingMode, Is.False);
            Assert.That(_game.Markers[0, 2], Is.EqualTo(VoltorbFlipMarkerType.None));
        }
    }

    #endregion

    #region QuitRound

    [Test]
    public async Task Test_QuitRound_ShouldDoNothing_WhenUserIsNotOwner()
    {
        await _game.StartNewRound();
        var otherUser = Substitute.For<IUser>();
        otherUser.UserId.Returns("otherplayer");

        await _game.QuitRound(otherUser);

        Assert.That(_game.IsRoundActive, Is.True);
    }

    [Test]
    public async Task Test_QuitRound_ShouldDoNothing_WhenRoundNotActive()
    {
        await _game.QuitRound(_mockUser);

        _mockContext.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_QuitRound_ShouldEndGame()
    {
        await _game.StartNewRound();

        await _game.QuitRound(_mockUser);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsRoundActive, Is.False);
            Assert.That(_game.IsEnded, Is.True);
        }
    }

    [Test]
    public async Task Test_QuitRound_ShouldSendQuitMessage_WithZeroCoins_WhenNoTilesFlipped()
    {
        await _game.StartNewRound();

        await _game.QuitRound(_mockUser);

        _mockContext.Received(1).ReplyLocalizedMessage("vf_game_quit", _mockUser.Name, Arg.Is(0), Arg.Any<int>());
    }

    [Test]
    public async Task Test_QuitRound_ShouldSendQuitMessage_WithEarnedCoins_WhenTilesFlipped()
    {
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 2); // 1x tile → coins = 1

        await _game.QuitRound(_mockUser);

        _mockContext.Received(1).ReplyLocalizedMessage("vf_game_quit", _mockUser.Name, 1, Arg.Any<int>());
    }

    [Test]
    public async Task Test_QuitRound_ShouldKeepLevel_WhenRevealedCountMeetsCurrentLevel()
    {
        // At level 1: reveal 1 non-Voltorb tile (revealed=1 >= level=1) → keep level
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 2); // 1x tile

        await _game.QuitRound(_mockUser);

        Assert.That(_game.Level, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_QuitRound_ShouldDropLevel_WhenRevealedCountBelowCurrentLevel()
    {
        // Win once (level → 2), then quit immediately on new round → 0 < 2 → drop to 1
        await _game.StartNewRound();
        await _game.FlipTile(_mockUser, 0, 1); // Win
        await _game.StartNewRound();

        await _game.QuitRound(_mockUser);

        Assert.That(_game.Level, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_QuitRound_ShouldSaveLevelToDb_WhenQuit()
    {
        await _game.StartNewRound();

        await _game.QuitRound(_mockUser);

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.VoltorbFlipLevels.FindAsync("testplayer");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record, Is.Not.Null);
            Assert.That(record.Level, Is.EqualTo(1));
        }
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
    public async Task Test_CancelAsync_ShouldWork_WhenNoRoundIsActive()
    {
        await _game.StartNewRound();
        await _game.QuitRound(_mockUser);

        Assert.DoesNotThrowAsync(async () => await _game.CancelAsync());
        Assert.That(_game.IsEnded, Is.True);
    }

    [Test]
    public async Task Test_CancelAsync_ShouldSaveLevelToDb_WhenOwnerIsSet()
    {
        await _game.StartNewRound();

        await _game.CancelAsync();

        await using var db = new BotDbContext(_dbOptions);
        var record = await db.VoltorbFlipLevels.FindAsync("testplayer");
        Assert.That(record, Is.Not.Null);
    }

    #endregion
}
