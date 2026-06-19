using ElsaMina.Commands.Games.Chess;

namespace ElsaMina.UnitTests.Commands.Games.Chess;

public class ChessBoardTest
{
    private ChessBoard _board;

    [SetUp]
    public void SetUp()
    {
        _board = new ChessBoard();
        _board.Initialize();
    }

    [Test]
    public void Test_Initialize_ShouldPlaceAllPieces()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_board.Squares[0, 0], Is.EqualTo('r'));
            Assert.That(_board.Squares[0, 4], Is.EqualTo('k'));
            Assert.That(_board.Squares[1, 3], Is.EqualTo('p'));
            Assert.That(_board.Squares[6, 3], Is.EqualTo('P'));
            Assert.That(_board.Squares[7, 4], Is.EqualTo('K'));
            Assert.That(_board.Squares[7, 0], Is.EqualTo('R'));
            Assert.That(_board.Squares[4, 4], Is.EqualTo(ChessBoard.EMPTY));
            Assert.That(_board.WhiteToMove, Is.True);
        }
    }

    [Test]
    public void Test_TryParseSquare_ShouldRoundTrip()
    {
        Assert.That(ChessBoard.TryParseSquare("e4", out var row, out var column), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(row, Is.EqualTo(4));
            Assert.That(column, Is.EqualTo(4));
            Assert.That(ChessBoard.ToSquareName(row, column), Is.EqualTo("e4"));
        }
    }

    [Test]
    [TestCase("e9")]
    [TestCase("i1")]
    [TestCase("e")]
    [TestCase("")]
    public void Test_TryParseSquare_ShouldFail_WhenInputIsInvalid(string square)
    {
        Assert.That(ChessBoard.TryParseSquare(square, out _, out _), Is.False);
    }

    [Test]
    public void Test_GenerateLegalMoves_ShouldReturnTwenty_FromStartingPosition()
    {
        Assert.That(_board.GenerateLegalMoves(true), Has.Count.EqualTo(20));
    }

    [Test]
    public void Test_GenerateLegalMoves_ShouldIncludePawnDoublePush()
    {
        var moves = _board.GenerateLegalMoves(true);
        Assert.That(moves, Has.Some.Matches<ChessMove>(move =>
            move.FromRow == 6 && move.FromColumn == 4 && move.ToRow == 4 && move.ToColumn == 4));
    }

    [Test]
    public void Test_ApplyMove_ShouldSetEnPassantTarget_AfterDoublePush()
    {
        ApplyCoordinateMove("e2e4");
        // The en passant target is the skipped square e3, not the landing square.
        Assert.That(_board.EnPassantTarget, Is.EqualTo((5, 4)));
    }

    [Test]
    public void Test_EnPassant_ShouldCapturePawn()
    {
        ApplyCoordinateMove("e2e4");
        ApplyCoordinateMove("a7a6");
        ApplyCoordinateMove("e4e5");
        ApplyCoordinateMove("d7d5"); // Enables en passant on d6
        ApplyCoordinateMove("e5d6"); // White captures en passant

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_board.Squares[2, 3], Is.EqualTo('P')); // White pawn on d6
            Assert.That(_board.Squares[3, 3], Is.EqualTo(ChessBoard.EMPTY)); // Captured black pawn gone
            Assert.That(_board.Squares[3, 4], Is.EqualTo(ChessBoard.EMPTY)); // e5 vacated
        }
    }

    [Test]
    public void Test_Castling_ShouldMoveKingAndRook_Kingside()
    {
        _board.Squares[7, 5] = ChessBoard.EMPTY; // Remove white bishop f1
        _board.Squares[7, 6] = ChessBoard.EMPTY; // Remove white knight g1

        var castle = _board.GenerateLegalMoves(true)
            .FirstOrDefault(move => move.IsCastle && move.ToColumn == 6);
        Assert.That(castle, Is.Not.Null);

        _board.ApplyMove(castle);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_board.Squares[7, 6], Is.EqualTo('K'));
            Assert.That(_board.Squares[7, 5], Is.EqualTo('R'));
            Assert.That(_board.Squares[7, 4], Is.EqualTo(ChessBoard.EMPTY));
            Assert.That(_board.Squares[7, 7], Is.EqualTo(ChessBoard.EMPTY));
        }
    }

    [Test]
    public void Test_Promotion_ShouldReplacePawnWithChosenPiece()
    {
        ClearBoard();
        _board.Squares[1, 0] = 'P'; // White pawn on a7
        _board.Squares[7, 7] = 'K';
        _board.Squares[0, 4] = 'k';

        var promotions = _board.GenerateLegalMoves(true)
            .Where(move => move.FromRow == 1 && move.FromColumn == 0 && move.ToRow == 0 && move.ToColumn == 0)
            .ToList();
        Assert.That(promotions, Has.Count.EqualTo(4));

        var queenPromotion = promotions.First(move => char.ToLowerInvariant(move.Promotion) == 'q');
        _board.ApplyMove(queenPromotion);
        Assert.That(_board.Squares[0, 0], Is.EqualTo('Q'));
    }

    [Test]
    public void Test_IsCheckmate_ShouldDetectMate()
    {
        ClearBoard();
        _board.Squares[0, 0] = 'k'; // Black king a8
        _board.Squares[1, 1] = 'Q'; // White queen b7
        _board.Squares[2, 1] = 'K'; // White king b6 defends the queen

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_board.IsInCheck(false), Is.True);
            Assert.That(_board.IsCheckmate(false), Is.True);
            Assert.That(_board.IsStalemate(false), Is.False);
        }
    }

    [Test]
    public void Test_IsStalemate_ShouldDetectStalemate()
    {
        ClearBoard();
        _board.Squares[0, 0] = 'k'; // Black king a8
        _board.Squares[2, 1] = 'Q'; // White queen b6
        _board.Squares[2, 2] = 'K'; // White king c6

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_board.IsInCheck(false), Is.False);
            Assert.That(_board.IsStalemate(false), Is.True);
            Assert.That(_board.IsCheckmate(false), Is.False);
        }
    }

    [Test]
    public void Test_GenerateLegalMoves_ShouldNotLeaveKingInCheck()
    {
        ClearBoard();
        _board.Squares[7, 4] = 'K'; // White king e1
        _board.Squares[6, 4] = 'B'; // White bishop e2 (pinned)
        _board.Squares[0, 4] = 'r'; // Black rook e8 pins the bishop
        _board.Squares[0, 0] = 'k';

        var bishopMoves = _board.GenerateLegalMoves(true)
            .Where(move => move.FromRow == 6 && move.FromColumn == 4);
        // The pinned bishop can only stay on the e-file (no legal off-file move).
        Assert.That(bishopMoves, Is.Empty);
    }

    private void ClearBoard()
    {
        for (var row = 0; row < ChessConstants.BOARD_SIZE; row++)
        {
            for (var column = 0; column < ChessConstants.BOARD_SIZE; column++)
            {
                _board.Squares[row, column] = ChessBoard.EMPTY;
            }
        }
    }

    private void ApplyCoordinateMove(string coordinates)
    {
        ChessBoard.TryParseSquare(coordinates[..2], out var fromRow, out var fromColumn);
        ChessBoard.TryParseSquare(coordinates.Substring(2, 2), out var toRow, out var toColumn);
        var move = _board.GenerateLegalMoves(_board.WhiteToMove)
            .First(candidate => candidate.FromRow == fromRow
                                && candidate.FromColumn == fromColumn
                                && candidate.ToRow == toRow
                                && candidate.ToColumn == toColumn);
        _board.ApplyMove(move);
    }
}
