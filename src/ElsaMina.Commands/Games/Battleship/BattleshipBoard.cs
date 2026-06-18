namespace ElsaMina.Commands.Games.Battleship;

public class BattleshipBoard
{
    public BattleshipShip[,] ShipGrid { get; } =
        new BattleshipShip[BattleshipConstants.BOARD_SIZE, BattleshipConstants.BOARD_SIZE];

    public CellShotState[,] Shots { get; } =
        new CellShotState[BattleshipConstants.BOARD_SIZE, BattleshipConstants.BOARD_SIZE];

    public List<BattleshipShip> Ships { get; } = [];

    public (int Row, int Column) LastShot { get; set; } = (-1, -1);

    public bool AllShipsSunk => Ships.Count > 0 && Ships.All(ship => ship.IsSunk);
}
