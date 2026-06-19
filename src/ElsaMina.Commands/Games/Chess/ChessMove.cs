namespace ElsaMina.Commands.Games.Chess;

/// <summary>
/// Represents a single chess move from one square to another.
/// Rows and columns are 0-based: row 0 is rank 8 (top), row 7 is rank 1 (bottom),
/// column 0 is file a, column 7 is file h.
/// </summary>
public record ChessMove(
    int FromRow,
    int FromColumn,
    int ToRow,
    int ToColumn,
    char Promotion = ChessBoard.EMPTY,
    bool IsEnPassant = false,
    bool IsCastle = false);
