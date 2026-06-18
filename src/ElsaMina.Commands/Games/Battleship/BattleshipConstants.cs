namespace ElsaMina.Commands.Games.Battleship;

public static class BattleshipConstants
{
    public const int BOARD_SIZE = 10;
    public const int MAX_PLAYERS_COUNT = 2;

    public static readonly TimeSpan TIMEOUT_DELAY = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan PLACEMENT_TIMEOUT_DELAY = TimeSpan.FromSeconds(120);

    public static readonly IReadOnlyList<BattleshipShipType> FLEET =
    [
        new("carrier", 5),
        new("battleship", 4),
        new("cruiser", 3),
        new("submarine", 3),
        new("destroyer", 2)
    ];

    public static char ColumnLabel(int columnIndex) => (char)('A' + columnIndex);

    public static string FormatCoordinate(int row, int column) => $"{ColumnLabel(column)}{row + 1}";
}
