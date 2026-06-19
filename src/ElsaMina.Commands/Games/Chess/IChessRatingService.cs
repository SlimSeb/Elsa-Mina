using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Chess;

public record ChessRatingChange(int OldRating, int NewRating)
{
    public int Delta => NewRating - OldRating;
}

public interface IChessRatingService
{
    Task<(ChessRatingChange, ChessRatingChange)> UpdateRatingsOnWinAsync(IUser winner, IUser loser, CancellationToken cancellationToken = default);

    Task<(ChessRatingChange, ChessRatingChange)> UpdateRatingsOnDrawAsync(IUser player1, IUser player2, CancellationToken cancellationToken = default);
}
