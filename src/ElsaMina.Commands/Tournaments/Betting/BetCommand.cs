using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Tournaments.Betting;

[NamedCommand("bet")]
public class BetCommand : Command
{
    private readonly ITournamentBettingService _tournamentBettingService;

    public BetCommand(ITournamentBettingService tournamentBettingService)
    {
        _tournamentBettingService = tournamentBettingService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            context.ReplyLocalizedMessage("bet_help");
            return;
        }

        var roomId = parts[0].Trim();
        var targetPlayer = parts[1].ToLowerAlphaNum();
        var bettorId = context.Sender.UserId.ToLowerAlphaNum();

        var result = await _tournamentBettingService.PlaceBetAsync(bettorId, targetPlayer, roomId, cancellationToken);

        switch (result)
        {
            case BetPlacementError.Success:
                context.ReplyLocalizedMessage("bet_success", targetPlayer, roomId);
                break;
            case BetPlacementError.NoBettingSession:
                context.ReplyLocalizedMessage("bet_no_session", roomId);
                break;
            case BetPlacementError.BettingClosed:
                context.ReplyLocalizedMessage("bet_closed");
                break;
            case BetPlacementError.InvalidPlayer:
                context.ReplyLocalizedMessage("bet_invalid_player", targetPlayer);
                break;
            case BetPlacementError.AlreadyBet:
                context.ReplyLocalizedMessage("bet_already_placed");
                break;
            default:
                throw new NotImplementedException("Unhandled bet placement error");
        }
    }
}
