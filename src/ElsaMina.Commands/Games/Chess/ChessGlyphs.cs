namespace ElsaMina.Commands.Games.Chess;

public static class ChessGlyphs
{
    private static readonly Dictionary<char, string> Glyphs = new()
    {
        ['K'] = "♔", ['Q'] = "♕", ['R'] = "♖", ['B'] = "♗", ['N'] = "♘", ['P'] = "♙",
        ['k'] = "♚", ['q'] = "♛", ['r'] = "♜", ['b'] = "♝", ['n'] = "♞", ['p'] = "♟"
    };

    public static string GetGlyph(char piece) => Glyphs.GetValueOrDefault(piece, string.Empty);
}
