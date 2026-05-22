using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;

namespace ElsaMina.Commands.Arcade.Levels;

[NamedCommand("deletepalier", "removepalier", "removelevel")]
public class DeleteArcadeLevelCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public DeleteArcadeLevelCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string[] RoomRestriction => ["arcade", "botdevelopment"];
    public override string HelpMessageKey => "arcade_level_delete_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var id = context.Target.ToLowerAlphaNum();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var arcadeLevel = await dbContext.ArcadeLevels.FindAsync([id], cancellationToken);
        if (arcadeLevel == null)
        {
            context.ReplyLocalizedMessage("arcade_level_delete_not_found");
        }
        else
        {
            try
            {
                dbContext.Remove(arcadeLevel);
                await dbContext.SaveChangesAsync(cancellationToken);
                context.ReplyLocalizedMessage("arcade_level_delete_success");
            }
            catch (Exception e)
            {
                context.ReplyLocalizedMessage("arcade_level_delete_failure", e.Message);
            }
        }
    }
}