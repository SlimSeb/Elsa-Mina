namespace ElsaMina.Commands.Games.Battleship;

public class BattleshipShip
{
    public string NameKey { get; init; }
    public int Size { get; init; }
    public List<(int Row, int Column)> Cells { get; } = [];
    public int Hits { get; set; }
    public bool IsSunk => Hits >= Size;
}
