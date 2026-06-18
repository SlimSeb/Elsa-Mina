using ElsaMina.Commands.Games.Battleship;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Battleship;

public class BattleshipGameTest
{
    private IRandomService _randomService;
    private ITemplatesManager _mockTemplatesManager;
    private IConfiguration _configuration;
    private IBattleshipRatingService _mockRatingService;
    private IContext _context;
    private IUser _mockUser1;
    private IUser _mockUser2;

    [SetUp]
    public void SetUp()
    {
        // Real random service so ship placement actually resolves; tests introspect the boards.
        _randomService = new RandomService();
        _mockTemplatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();
        _mockRatingService = Substitute.For<IBattleshipRatingService>();

        _configuration.Name.Returns("Bot");
        _configuration.Trigger.Returns("!");
        _mockTemplatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));
        _mockRatingService.UpdateRatingsOnWinAsync(Arg.Any<IUser>(), Arg.Any<IUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(BattleshipRatingChange, BattleshipRatingChange)>(
                (new BattleshipRatingChange(1000, 1016), new BattleshipRatingChange(1000, 984))));

        _mockUser1 = Substitute.For<IUser>();
        _mockUser2 = Substitute.For<IUser>();
        _mockUser1.UserId.Returns("player1");
        _mockUser2.UserId.Returns("player2");
        _mockUser1.Name.Returns("Player1");
        _mockUser2.Name.Returns("Player2");
    }

    private BattleshipGame CreateGame(TimeSpan? turnTimeout = null, TimeSpan? placementTimeout = null)
    {
        var game = new BattleshipGame(_randomService, _mockTemplatesManager, _configuration, _mockRatingService,
            turnTimeout ?? TimeSpan.FromMinutes(5), placementTimeout ?? TimeSpan.FromMinutes(5));
        game.Context = _context;
        return game;
    }

    private async Task<BattleshipGame> CreatePlayingGameAsync(TimeSpan? turnTimeout = null)
    {
        var game = CreateGame(turnTimeout);
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);
        await game.RandomPlaceRemaining(_mockUser1);
        await game.RandomPlaceRemaining(_mockUser2); // Both fleets ready: playing phase begins.
        return game;
    }

    [Test]
    public async Task Test_JoinGame_ShouldEnterPlacementPhase_WhenTwoPlayersJoin()
    {
        // Arrange
        var game = CreateGame();

        // Act
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.Players, Has.Count.EqualTo(2));
            Assert.That(game.IsPlacementPhase, Is.True);
            Assert.That(game.IsStarted, Is.False);
            Assert.That(game.TurnCount, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Test_JoinGame_ShouldNotExceedMaxPlayers_WhenMultiplePlayersJoinConcurrently()
    {
        // Arrange
        var game = CreateGame();
        var mockUser3 = Substitute.For<IUser>();
        mockUser3.UserId.Returns("player3");

        // Act
        await Task.WhenAll(
            game.JoinGame(_mockUser1),
            game.JoinGame(_mockUser2),
            game.JoinGame(mockUser3));

        // Assert
        Assert.That(game.Players, Has.Count.EqualTo(BattleshipConstants.MAX_PLAYERS_COUNT));
    }

    [Test]
    public async Task Test_PlaceShip_ShouldPlaceCurrentShipAndAdvance_WhenPlacementIsValid()
    {
        // Arrange
        var game = CreateGame();
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);
        var player = game.Players.First(candidate => Equals(candidate.User, _mockUser1));

        // Act: first ship is the carrier (size 5), default horizontal orientation, placed from A1.
        await game.PlaceShip(_mockUser1, "A1");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(player.Board.Ships, Has.Count.EqualTo(1));
            Assert.That(player.NextShipIndex, Is.EqualTo(1));
            for (var column = 0; column < BattleshipConstants.FLEET[0].Size; column++)
            {
                Assert.That(player.Board.ShipGrid[0, column], Is.Not.Null);
            }
        }
    }

    [Test]
    public async Task Test_PlaceShip_ShouldBeIgnored_WhenShipsOverlap()
    {
        // Arrange
        var game = CreateGame();
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);
        var player = game.Players.First(candidate => Equals(candidate.User, _mockUser1));
        await game.PlaceShip(_mockUser1, "A1"); // Carrier on row 0

        // Act: next ship overlaps the carrier
        await game.PlaceShip(_mockUser1, "A1");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(player.Board.Ships, Has.Count.EqualTo(1));
            Assert.That(player.NextShipIndex, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_ToggleOrientation_ShouldFlipPlacementOrientation()
    {
        // Arrange
        var game = CreateGame();
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);
        var player = game.Players.First(candidate => Equals(candidate.User, _mockUser1));
        var initial = player.IsHorizontalPlacement;

        // Act
        await game.ToggleOrientation(_mockUser1);

        // Assert
        Assert.That(player.IsHorizontalPlacement, Is.EqualTo(!initial));
    }

    [Test]
    public async Task Test_RandomPlaceRemaining_ShouldPlaceFullFleet()
    {
        // Arrange
        var game = CreateGame();
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);
        var player = game.Players.First(candidate => Equals(candidate.User, _mockUser1));

        // Act
        await game.RandomPlaceRemaining(_mockUser1);

        // Assert
        var expectedCells = BattleshipConstants.FLEET.Sum(ship => ship.Size);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(player.HasPlacedAllShips, Is.True);
            Assert.That(player.Board.Ships, Has.Count.EqualTo(BattleshipConstants.FLEET.Count));
            Assert.That(CountShipCells(player.Board), Is.EqualTo(expectedCells));
            Assert.That(game.IsStarted, Is.False); // opponent has not placed yet
        }
    }

    [Test]
    public async Task Test_Game_ShouldStartPlaying_WhenBothPlayersHavePlacedFleets()
    {
        // Arrange & Act
        var game = await CreatePlayingGameAsync();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.IsStarted, Is.True);
            Assert.That(game.IsPlacementPhase, Is.False);
            Assert.That(game.TurnCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_Fire_ShouldBeIgnored_DuringPlacementPhase()
    {
        // Arrange
        var game = CreateGame();
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);

        // Act
        await game.Fire(_mockUser1, "A1");

        // Assert
        Assert.That(game.TurnCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_Fire_ShouldBeIgnored_WhenNotPlayersTurn()
    {
        // Arrange
        var game = await CreatePlayingGameAsync();
        var notCurrent = game.Players.First(player => !Equals(player.User, game.PlayerCurrentlyPlaying));

        // Act
        await game.Fire(notCurrent.User, "A1");

        // Assert
        Assert.That(game.TurnCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_Fire_ShouldMarkMiss_WhenCellHasNoShip()
    {
        // Arrange
        var game = await CreatePlayingGameAsync();
        var current = CurrentPlayer(game);
        var opponent = Opponent(game, current);
        var waterCell = FindWaterCell(opponent.Board);

        // Act
        await game.Fire(current.User, BattleshipConstants.FormatCoordinate(waterCell.Row, waterCell.Column));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(opponent.Board.Shots[waterCell.Row, waterCell.Column], Is.EqualTo(CellShotState.Miss));
            Assert.That(game.TurnCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Test_Fire_ShouldMarkHit_WhenCellHasShip()
    {
        // Arrange
        var game = await CreatePlayingGameAsync();
        var current = CurrentPlayer(game);
        var opponent = Opponent(game, current);
        var shipCell = FindShipCell(opponent.Board);

        // Act
        await game.Fire(current.User, BattleshipConstants.FormatCoordinate(shipCell.Row, shipCell.Column));

        // Assert
        Assert.That(opponent.Board.Shots[shipCell.Row, shipCell.Column], Is.EqualTo(CellShotState.Hit));
    }

    [Test]
    public async Task Test_Game_ShouldDeclareWinner_WhenAllOpponentShipsAreSunk()
    {
        // Arrange
        var game = await CreatePlayingGameAsync();

        // Act: Player1 hits every turn, Player2 always misses, so Player1 wins.
        await PlayUntilWinnerAsync(game, _mockUser1);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.IsEnded, Is.True);
            Assert.That(game.WinnerName, Is.EqualTo(_mockUser1.Name));
        }

        await _mockRatingService.Received(1)
            .UpdateRatingsOnWinAsync(_mockUser1, _mockUser2, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_Forfeit_ShouldMakeOpponentWin_DuringPlacement()
    {
        // Arrange
        var game = CreateGame();
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);

        // Act
        await game.Forfeit(_mockUser1);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.IsEnded, Is.True);
            Assert.That(game.WinnerName, Is.EqualTo(_mockUser2.Name));
        }
    }

    [Test]
    public async Task Test_PlacementTimeout_ShouldAutoPlaceFleetsAndStart_WhenDelayElapses()
    {
        // Arrange
        var game = CreateGame(placementTimeout: TimeSpan.FromMilliseconds(40));
        await game.JoinGame(_mockUser1);
        await game.JoinGame(_mockUser2);

        // Act
        await Task.Delay(150);

        // Assert
        var expectedCells = BattleshipConstants.FLEET.Sum(ship => ship.Size);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.IsStarted, Is.True);
            Assert.That(game.IsPlacementPhase, Is.False);
            foreach (var player in game.Players)
            {
                Assert.That(CountShipCells(player.Board), Is.EqualTo(expectedCells));
            }
        }
    }

    [Test]
    public async Task Test_Timeout_ShouldDisqualifyCurrentPlayer_WhenDelayElapses()
    {
        // Arrange
        var game = await CreatePlayingGameAsync(turnTimeout: TimeSpan.FromMilliseconds(40));
        var current = game.PlayerCurrentlyPlaying;

        // Act
        await Task.Delay(150);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.IsEnded, Is.True);
            Assert.That(game.WinnerName, Is.Not.EqualTo(current.Name)); // the timed-out player loses
        }
    }

    [Test]
    public async Task Test_Timeout_ShouldBeCancelled_WhenGameIsCancelled()
    {
        // Arrange
        var game = await CreatePlayingGameAsync(turnTimeout: TimeSpan.FromMilliseconds(40));

        // Act
        game.Cancel();
        await Task.Delay(150);

        // Assert: the turn timeout must not have fired (no winner was declared by timeout)
        Assert.That(game.WinnerName, Is.Null);
    }

    private async Task PlayUntilWinnerAsync(BattleshipGame game, IUser intendedWinner)
    {
        var safety = 0;
        while (!game.IsEnded && safety++ < 500)
        {
            var current = CurrentPlayer(game);
            var opponent = Opponent(game, current);

            if (Equals(current.User, intendedWinner))
            {
                var shipCell = FindShipCell(opponent.Board);
                await game.Fire(current.User, BattleshipConstants.FormatCoordinate(shipCell.Row, shipCell.Column));
            }
            else
            {
                var waterCell = FindWaterCell(opponent.Board);
                await game.Fire(current.User, BattleshipConstants.FormatCoordinate(waterCell.Row, waterCell.Column));
            }
        }
    }

    private static BattleshipPlayer CurrentPlayer(IBattleshipGame game) =>
        game.Players.First(player => Equals(player.User, game.PlayerCurrentlyPlaying));

    private static BattleshipPlayer Opponent(IBattleshipGame game, BattleshipPlayer player) =>
        game.Players.First(other => !Equals(other.User, player.User));

    private static int CountShipCells(BattleshipBoard board)
    {
        var count = 0;
        for (var row = 0; row < BattleshipConstants.BOARD_SIZE; row++)
        {
            for (var column = 0; column < BattleshipConstants.BOARD_SIZE; column++)
            {
                if (board.ShipGrid[row, column] is not null)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static (int Row, int Column) FindShipCell(BattleshipBoard board)
    {
        for (var row = 0; row < BattleshipConstants.BOARD_SIZE; row++)
        {
            for (var column = 0; column < BattleshipConstants.BOARD_SIZE; column++)
            {
                if (board.ShipGrid[row, column] is not null && board.Shots[row, column] == CellShotState.None)
                {
                    return (row, column);
                }
            }
        }

        throw new InvalidOperationException("No un-hit ship cell available.");
    }

    private static (int Row, int Column) FindWaterCell(BattleshipBoard board)
    {
        for (var row = 0; row < BattleshipConstants.BOARD_SIZE; row++)
        {
            for (var column = 0; column < BattleshipConstants.BOARD_SIZE; column++)
            {
                if (board.ShipGrid[row, column] is null && board.Shots[row, column] == CellShotState.None)
                {
                    return (row, column);
                }
            }
        }

        throw new InvalidOperationException("No un-shot water cell available.");
    }
}
