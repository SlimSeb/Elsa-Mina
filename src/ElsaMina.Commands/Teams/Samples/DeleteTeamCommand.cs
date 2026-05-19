using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Teams.Samples;

[NamedCommand("delete-team", Aliases = ["deleteteam"])]
public class DeleteTeamCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public DeleteTeamCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "deleteteam_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var team = await dbContext.Teams.FindAsync([context.Target?.ToLowerAlphaNum()], cancellationToken);
        if (team == null)
        {
            context.ReplyLocalizedMessage("deleteteam_team_not_found");
            return;
        }

        try
        {
            dbContext.Remove(team);
            await dbContext.SaveChangesAsync(cancellationToken);
            context.ReplyLocalizedMessage("deleteteam_team_deleted_successfully");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while deleting team");
            context.ReplyLocalizedMessage("deleteteam_team_deletion_error", exception.Message);
        }
    }
}