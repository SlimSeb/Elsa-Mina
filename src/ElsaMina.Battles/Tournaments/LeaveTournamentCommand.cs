using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Battles.Tournaments;

/// <summary>
/// Makes the bot leave the room's tournament, forfeiting it once started.
/// </summary>
[NamedCommand("leavetour", Aliases = ["tourleave"])]
public class LeaveTournamentCommand : Command
{
    private readonly ITournamentPlayingService _tournamentPlayingService;

    public LeaveTournamentCommand(ITournamentPlayingService tournamentPlayingService)
    {
        _tournamentPlayingService = tournamentPlayingService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "leavetour_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        context.ReplyLocalizedMessage(_tournamentPlayingService.Leave(context.RoomId)
            ? "leavetour_leaving"
            : "leavetour_not_joined");
        return Task.CompletedTask;
    }
}
