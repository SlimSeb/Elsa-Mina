using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Battleship;

public interface IBattleshipRatingService
{
    Task<(BattleshipRatingChange, BattleshipRatingChange)> UpdateRatingsOnWinAsync(IUser winner, IUser loser,
        CancellationToken cancellationToken = default);
}
