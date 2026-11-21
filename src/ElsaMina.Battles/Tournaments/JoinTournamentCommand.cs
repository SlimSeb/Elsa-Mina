using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Battles.Tournaments;

/// <summary>
/// Makes the bot join the room's tournament, whose matches it then plays.
/// </summary>
[NamedCommand("jointour", Aliases = ["tourjoin"])]
public class JoinTournamentCommand : Command
{
    private readonly ITournamentPlayingService _tournamentPlayingService;

    public JoinTournamentCommand(ITournamentPlayingService tournamentPlayingService)
    {
        _tournamentPlayingService = tournamentPlayingService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "jointour_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        switch (_tournamentPlayingService.Join(context.RoomId))
        {
            case TournamentJoinResult.Joining:
                context.ReplyLocalizedMessage("jointour_joining");
                break;
            case TournamentJoinResult.NoTournament:
                context.ReplyLocalizedMessage("jointour_no_tournament");
                break;
            case TournamentJoinResult.AlreadyJoined:
                context.ReplyLocalizedMessage("jointour_already_joined");
                break;
            case TournamentJoinResult.AlreadyStarted:
                context.ReplyLocalizedMessage("jointour_already_started");
                break;
            case TournamentJoinResult.UnsupportedFormat:
                context.ReplyLocalizedMessage("jointour_unsupported_format",
                    string.Join(", ", TournamentPlayingService.SUPPORTED_FORMAT_IDS.Order(StringComparer.Ordinal)));
                break;
        }

        return Task.CompletedTask;
    }
}
