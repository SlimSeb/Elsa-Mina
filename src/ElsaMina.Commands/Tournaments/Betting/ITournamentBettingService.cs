using ElsaMina.Commands.Tournaments.Handlers;

namespace ElsaMina.Commands.Tournaments.Betting;

public interface ITournamentBettingService
{
    Task AnnounceBetsAsync(TournamentPlayer[] players, string roomId, CancellationToken cancellationToken = default);
    Task<BetPlacementError> PlaceBetAsync(string bettorId, string targetPlayerId, string roomId,
        CancellationToken cancellationToken = default);
    Task<int> CancelBetAsync(string bettorId, string roomId, string targetPlayerId = null, CancellationToken cancellationToken = default);
    Task ResolveBetsAsync(string winnerId, string roomId, CancellationToken cancellationToken = default);
    Task ReturnBetsAsync(string roomId, CancellationToken cancellationToken = default);
}