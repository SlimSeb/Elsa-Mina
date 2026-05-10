using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Teams.Samples;

[NamedCommand("deleteallteams", Aliases = ["delete-all-teams"])]
public class DeleteAllTeamsByTierCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public DeleteAllTeamsByTierCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Mod;
    public override string HelpMessageKey => "deleteallteams_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var tier = context.Target?.Trim();
        if (string.IsNullOrEmpty(tier))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var normalizedTier = tier.ToLowerAlphaNum();

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var roomTeamsToDelete = (await dbContext.RoomTeams
                    .Include(roomTeam => roomTeam.Team)
                        .ThenInclude(team => team.Rooms)
                    .Where(roomTeam => roomTeam.RoomId == context.RoomId)
                    .ToListAsync(cancellationToken))
                .Where(roomTeam => roomTeam.Team.Format.ToLowerAlphaNum() == normalizedTier)
                .ToList();

            if (roomTeamsToDelete.Count == 0)
            {
                context.ReplyLocalizedMessage("deleteallteams_no_teams_found", tier);
                return;
            }

            foreach (var roomTeam in roomTeamsToDelete)
            {
                dbContext.RoomTeams.Remove(roomTeam);
                if (roomTeam.Team.Rooms.Count == 1)
                {
                    dbContext.Teams.Remove(roomTeam.Team);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            context.ReplyLocalizedMessage("deleteallteams_success", roomTeamsToDelete.Count, tier);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while deleting all teams for tier {0} in room {1}",
                tier, context.RoomId);
            context.ReplyLocalizedMessage("deleteallteams_error", exception.Message);
        }
    }
}
