namespace ElsaMina.Commands.Games.Chess;

/// <summary>
/// A standard 8x8 chess board with full move legality, including check, checkmate,
/// stalemate, castling, en passant and promotion.
/// White pieces are uppercase, black pieces are lowercase, empty squares are <see cref="EMPTY"/>.
/// Row 0 is rank 8 (top), row 7 is rank 1 (bottom); column 0 is file a, column 7 is file h.
/// </summary>
public class ChessBoard
{
    public const char EMPTY = '.';

    private static readonly (int Row, int Column)[] KnightOffsets =
    [
        (-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1)
    ];

    private static readonly (int Row, int Column)[] KingOffsets =
    [
        (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)
    ];

    private static readonly (int Row, int Column)[] RookDirections = [(-1, 0), (1, 0), (0, -1), (0, 1)];
    private static readonly (int Row, int Column)[] BishopDirections = [(-1, -1), (-1, 1), (1, -1), (1, 1)];

    public char[,] Squares { get; private set; } = new char[ChessConstants.BOARD_SIZE, ChessConstants.BOARD_SIZE];
    public bool WhiteToMove { get; private set; } = true;
    public bool WhiteCanCastleKingside { get; private set; } = true;
    public bool WhiteCanCastleQueenside { get; private set; } = true;
    public bool BlackCanCastleKingside { get; private set; } = true;
    public bool BlackCanCastleQueenside { get; private set; } = true;
    public (int Row, int Column)? EnPassantTarget { get; private set; }
    public ChessMove LastMove { get; private set; }

    public ChessColor SideToMove => WhiteToMove ? ChessColor.White : ChessColor.Black;

    public void Initialize()
    {
        for (var row = 0; row < ChessConstants.BOARD_SIZE; row++)
        {
            for (var column = 0; column < ChessConstants.BOARD_SIZE; column++)
            {
                Squares[row, column] = EMPTY;
            }
        }

        const string backRank = "rnbqkbnr";
        for (var column = 0; column < ChessConstants.BOARD_SIZE; column++)
        {
            Squares[0, column] = backRank[column]; // Black back rank (rank 8)
            Squares[1, column] = 'p'; // Black pawns (rank 7)
            Squares[6, column] = 'P'; // White pawns (rank 2)
            Squares[7, column] = char.ToUpperInvariant(backRank[column]); // White back rank (rank 1)
        }

        WhiteToMove = true;
        WhiteCanCastleKingside = WhiteCanCastleQueenside = true;
        BlackCanCastleKingside = BlackCanCastleQueenside = true;
        EnPassantTarget = null;
        LastMove = null;
    }

    public static bool IsWhite(char piece) => piece != EMPTY && char.IsUpper(piece);
    public static bool IsBlack(char piece) => piece != EMPTY && char.IsLower(piece);
    public static bool IsEmpty(char piece) => piece == EMPTY;

    public static bool IsInsideBoard(int row, int column) =>
        row >= 0 && row < ChessConstants.BOARD_SIZE && column >= 0 && column < ChessConstants.BOARD_SIZE;

    private static bool BelongsTo(char piece, bool white) => white ? IsWhite(piece) : IsBlack(piece);

    public bool IsInCheck(bool white)
    {
        var (kingRow, kingColumn) = FindKing(white);
        return kingRow >= 0 && IsSquareAttacked(kingRow, kingColumn, !white);
    }

    public (int Row, int Column) FindKing(bool white)
    {
        var king = white ? 'K' : 'k';
        for (var row = 0; row < ChessConstants.BOARD_SIZE; row++)
        {
            for (var column = 0; column < ChessConstants.BOARD_SIZE; column++)
            {
                if (Squares[row, column] == king)
                {
                    return (row, column);
                }
            }
        }

        return (-1, -1);
    }

    /// <summary>
    /// Returns whether the given square is attacked by a piece of the given color.
    /// </summary>
    public bool IsSquareAttacked(int row, int column, bool byWhite)
    {
        // Pawn attacks: a white pawn attacks the squares diagonally above it (smaller row).
        var pawnRow = byWhite ? row + 1 : row - 1;
        var pawn = byWhite ? 'P' : 'p';
        if (IsInsideBoard(pawnRow, column - 1) && Squares[pawnRow, column - 1] == pawn)
        {
            return true;
        }

        if (IsInsideBoard(pawnRow, column + 1) && Squares[pawnRow, column + 1] == pawn)
        {
            return true;
        }

        // Knight attacks
        var knight = byWhite ? 'N' : 'n';
        foreach (var (rowOffset, columnOffset) in KnightOffsets)
        {
            var checkedRow = row + rowOffset;
            var checkedColumn = column + columnOffset;
            if (IsInsideBoard(checkedRow, checkedColumn) && Squares[checkedRow, checkedColumn] == knight)
            {
                return true;
            }
        }

        // King attacks
        var king = byWhite ? 'K' : 'k';
        foreach (var (rowOffset, columnOffset) in KingOffsets)
        {
            var checkedRow = row + rowOffset;
            var checkedColumn = column + columnOffset;
            if (IsInsideBoard(checkedRow, checkedColumn) && Squares[checkedRow, checkedColumn] == king)
            {
                return true;
            }
        }

        // Sliding attacks: rook / queen along ranks and files
        var rook = byWhite ? 'R' : 'r';
        var queen = byWhite ? 'Q' : 'q';
        if (IsAttackedFromDirections(row, column, RookDirections, rook, queen))
        {
            return true;
        }

        // Sliding attacks: bishop / queen along diagonals
        var bishop = byWhite ? 'B' : 'b';
        return IsAttackedFromDirections(row, column, BishopDirections, bishop, queen);
    }

    private bool IsAttackedFromDirections(int row, int column, (int Row, int Column)[] directions,
        char slider, char queen)
    {
        foreach (var (rowOffset, columnOffset) in directions)
        {
            var checkedRow = row + rowOffset;
            var checkedColumn = column + columnOffset;
            while (IsInsideBoard(checkedRow, checkedColumn))
            {
                var piece = Squares[checkedRow, checkedColumn];
                if (piece != EMPTY)
                {
                    if (piece == slider || piece == queen)
                    {
                        return true;
                    }

                    break;
                }

                checkedRow += rowOffset;
                checkedColumn += columnOffset;
            }
        }

        return false;
    }

    /// <summary>
    /// Generates every legal move for the given color (i.e. moves that do not leave the own king in check).
    /// </summary>
    public List<ChessMove> GenerateLegalMoves(bool white)
    {
        var legalMoves = new List<ChessMove>();
        foreach (var move in GeneratePseudoLegalMoves(white))
        {
            var clone = Clone();
            clone.ApplyMove(move);
            if (!clone.IsInCheck(white))
            {
                legalMoves.Add(move);
            }
        }

        return legalMoves;
    }

    private IEnumerable<ChessMove> GeneratePseudoLegalMoves(bool white)
    {
        var moves = new List<ChessMove>();
        for (var row = 0; row < ChessConstants.BOARD_SIZE; row++)
        {
            for (var column = 0; column < ChessConstants.BOARD_SIZE; column++)
            {
                var piece = Squares[row, column];
                if (piece == EMPTY || !BelongsTo(piece, white))
                {
                    continue;
                }

                switch (char.ToUpperInvariant(piece))
                {
                    case 'P':
                        AddPawnMoves(moves, row, column, white);
                        break;
                    case 'N':
                        AddOffsetMoves(moves, row, column, white, KnightOffsets);
                        break;
                    case 'K':
                        AddOffsetMoves(moves, row, column, white, KingOffsets);
                        AddCastlingMoves(moves, white);
                        break;
                    case 'B':
                        AddSlidingMoves(moves, row, column, white, BishopDirections);
                        break;
                    case 'R':
                        AddSlidingMoves(moves, row, column, white, RookDirections);
                        break;
                    case 'Q':
                        AddSlidingMoves(moves, row, column, white, BishopDirections);
                        AddSlidingMoves(moves, row, column, white, RookDirections);
                        break;
                }
            }
        }

        return moves;
    }

    private void AddOffsetMoves(List<ChessMove> moves, int row, int column, bool white,
        (int Row, int Column)[] offsets)
    {
        foreach (var (rowOffset, columnOffset) in offsets)
        {
            var toRow = row + rowOffset;
            var toColumn = column + columnOffset;
            if (!IsInsideBoard(toRow, toColumn))
            {
                continue;
            }

            var target = Squares[toRow, toColumn];
            if (target == EMPTY || !BelongsTo(target, white))
            {
                moves.Add(new ChessMove(row, column, toRow, toColumn));
            }
        }
    }

    private void AddSlidingMoves(List<ChessMove> moves, int row, int column, bool white,
        (int Row, int Column)[] directions)
    {
        foreach (var (rowOffset, columnOffset) in directions)
        {
            var toRow = row + rowOffset;
            var toColumn = column + columnOffset;
            while (IsInsideBoard(toRow, toColumn))
            {
                var target = Squares[toRow, toColumn];
                if (target == EMPTY)
                {
                    moves.Add(new ChessMove(row, column, toRow, toColumn));
                }
                else
                {
                    if (!BelongsTo(target, white))
                    {
                        moves.Add(new ChessMove(row, column, toRow, toColumn));
                    }

                    break;
                }

                toRow += rowOffset;
                toColumn += columnOffset;
            }
        }
    }

    private void AddPawnMoves(List<ChessMove> moves, int row, int column, bool white)
    {
        var direction = white ? -1 : 1;
        var startRow = white ? 6 : 1;
        var promotionRow = white ? 0 : 7;

        // Forward one square
        var oneStepRow = row + direction;
        if (IsInsideBoard(oneStepRow, column) && Squares[oneStepRow, column] == EMPTY)
        {
            AddPawnMoveWithPromotion(moves, row, column, oneStepRow, column, promotionRow);

            // Forward two squares from the starting rank
            var twoStepRow = row + 2 * direction;
            if (row == startRow && Squares[twoStepRow, column] == EMPTY)
            {
                moves.Add(new ChessMove(row, column, twoStepRow, column));
            }
        }

        // Captures (including en passant)
        foreach (var columnOffset in new[] { -1, 1 })
        {
            var toColumn = column + columnOffset;
            if (!IsInsideBoard(oneStepRow, toColumn))
            {
                continue;
            }

            var target = Squares[oneStepRow, toColumn];
            if (target != EMPTY && !BelongsTo(target, white))
            {
                AddPawnMoveWithPromotion(moves, row, column, oneStepRow, toColumn, promotionRow);
            }
            else if (EnPassantTarget == (oneStepRow, toColumn))
            {
                moves.Add(new ChessMove(row, column, oneStepRow, toColumn, IsEnPassant: true));
            }
        }
    }

    private static void AddPawnMoveWithPromotion(List<ChessMove> moves, int fromRow, int fromColumn,
        int toRow, int toColumn, int promotionRow)
    {
        if (toRow == promotionRow)
        {
            foreach (var promotion in new[] { 'q', 'r', 'b', 'n' })
            {
                moves.Add(new ChessMove(fromRow, fromColumn, toRow, toColumn, promotion));
            }
        }
        else
        {
            moves.Add(new ChessMove(fromRow, fromColumn, toRow, toColumn));
        }
    }

    private void AddCastlingMoves(List<ChessMove> moves, bool white)
    {
        var row = white ? 7 : 0;
        var king = white ? 'K' : 'k';
        var rook = white ? 'R' : 'r';

        // The king and rook must actually sit on their home squares.
        if (Squares[row, 4] != king)
        {
            return;
        }

        // The king must not currently be in check to castle.
        if (IsSquareAttacked(row, 4, !white))
        {
            return;
        }

        var canCastleKingside = white ? WhiteCanCastleKingside : BlackCanCastleKingside;
        var canCastleQueenside = white ? WhiteCanCastleQueenside : BlackCanCastleQueenside;

        if (canCastleKingside
            && Squares[row, 7] == rook
            && Squares[row, 5] == EMPTY
            && Squares[row, 6] == EMPTY
            && !IsSquareAttacked(row, 5, !white)
            && !IsSquareAttacked(row, 6, !white))
        {
            moves.Add(new ChessMove(row, 4, row, 6, IsCastle: true));
        }

        if (canCastleQueenside
            && Squares[row, 0] == rook
            && Squares[row, 3] == EMPTY
            && Squares[row, 2] == EMPTY
            && Squares[row, 1] == EMPTY
            && !IsSquareAttacked(row, 3, !white)
            && !IsSquareAttacked(row, 2, !white))
        {
            moves.Add(new ChessMove(row, 4, row, 2, IsCastle: true));
        }
    }

    /// <summary>
    /// Applies a move that is assumed to be legal, updating the board, side to move,
    /// castling rights and en passant target.
    /// </summary>
    public void ApplyMove(ChessMove move)
    {
        var piece = Squares[move.FromRow, move.FromColumn];
        var white = IsWhite(piece);

        Squares[move.FromRow, move.FromColumn] = EMPTY;

        if (move.IsEnPassant)
        {
            // The captured pawn sits on the moving pawn's row, in the destination column.
            Squares[move.FromRow, move.ToColumn] = EMPTY;
        }

        if (move.Promotion != EMPTY)
        {
            Squares[move.ToRow, move.ToColumn] = white
                ? char.ToUpperInvariant(move.Promotion)
                : char.ToLowerInvariant(move.Promotion);
        }
        else
        {
            Squares[move.ToRow, move.ToColumn] = piece;
        }

        if (move.IsCastle)
        {
            // Move the rook to the other side of the king.
            if (move.ToColumn == 6) // Kingside
            {
                Squares[move.FromRow, 5] = Squares[move.FromRow, 7];
                Squares[move.FromRow, 7] = EMPTY;
            }
            else if (move.ToColumn == 2) // Queenside
            {
                Squares[move.FromRow, 3] = Squares[move.FromRow, 0];
                Squares[move.FromRow, 0] = EMPTY;
            }
        }

        UpdateCastlingRights(move, piece);

        // Set en passant target only after a pawn double-push.
        if (char.ToUpperInvariant(piece) == 'P' && Math.Abs(move.ToRow - move.FromRow) == 2)
        {
            EnPassantTarget = ((move.FromRow + move.ToRow) / 2, move.FromColumn);
        }
        else
        {
            EnPassantTarget = null;
        }

        WhiteToMove = !white;
        LastMove = move;
    }

    private void UpdateCastlingRights(ChessMove move, char piece)
    {
        switch (piece)
        {
            case 'K':
                WhiteCanCastleKingside = WhiteCanCastleQueenside = false;
                break;
            case 'k':
                BlackCanCastleKingside = BlackCanCastleQueenside = false;
                break;
        }

        // A rook leaving (or being captured on) a home square removes the matching right.
        foreach (var (squareRow, squareColumn) in new[]
                 {
                     (move.FromRow, move.FromColumn), (move.ToRow, move.ToColumn)
                 })
        {
            switch (squareRow, squareColumn)
            {
                case (7, 0):
                    WhiteCanCastleQueenside = false;
                    break;
                case (7, 7):
                    WhiteCanCastleKingside = false;
                    break;
                case (0, 0):
                    BlackCanCastleQueenside = false;
                    break;
                case (0, 7):
                    BlackCanCastleKingside = false;
                    break;
            }
        }
    }

    public bool IsCheckmate(bool white) => IsInCheck(white) && GenerateLegalMoves(white).Count == 0;

    public bool IsStalemate(bool white) => !IsInCheck(white) && GenerateLegalMoves(white).Count == 0;

    public ChessBoard Clone()
    {
        var clone = new ChessBoard
        {
            Squares = (char[,])Squares.Clone(),
            WhiteToMove = WhiteToMove,
            WhiteCanCastleKingside = WhiteCanCastleKingside,
            WhiteCanCastleQueenside = WhiteCanCastleQueenside,
            BlackCanCastleKingside = BlackCanCastleKingside,
            BlackCanCastleQueenside = BlackCanCastleQueenside,
            EnPassantTarget = EnPassantTarget,
            LastMove = LastMove
        };
        return clone;
    }

    /// <summary>
    /// Parses a coordinate-notation square such as "e4" into board indices, or returns false.
    /// </summary>
    public static bool TryParseSquare(string square, out int row, out int column)
    {
        row = -1;
        column = -1;
        if (string.IsNullOrWhiteSpace(square) || square.Length != 2)
        {
            return false;
        }

        var file = char.ToLowerInvariant(square[0]);
        var rank = square[1];
        if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
        {
            return false;
        }

        column = file - 'a';
        row = '8' - rank;
        return true;
    }

    public static string ToSquareName(int row, int column) => $"{(char)('a' + column)}{(char)('8' - row)}";
}
