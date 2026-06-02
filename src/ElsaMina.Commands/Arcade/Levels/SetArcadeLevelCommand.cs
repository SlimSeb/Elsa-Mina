using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Arcade.Levels;

[NamedCommand("addpalier", "setpalier")]
public class SetArcadeLevelCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public SetArcadeLevelCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string[] RoomRestriction => ["arcade", "botdevelopment"];
    public override string HelpMessageKey => "arcade_level_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string user;
        int level;
        try
        {
            var parts = context.Target.Split(",");
            user = parts[0].ToLowerAlphaNum();
            level = int.Parse(parts[1]);
        }
        catch (Exception)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        if (level is < 1 or > 50)
        {
            context.ReplyLocalizedMessage("arcade_level_invalid_value");
            return;
        }

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var arcadeLevel = await dbContext.ArcadeLevels.FindAsync([user], cancellationToken);
            if (arcadeLevel == null)
            {
                await dbContext.EnsureUserExistsAsync(user, cancellationToken);
                await dbContext.ArcadeLevels.AddAsync(new ArcadeLevel
                {
                    Id = user,
                    Level = level
                }, cancellationToken);
                context.ReplyLocalizedMessage("arcade_level_add", user, level);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                arcadeLevel.Level = level;
                await dbContext.SaveChangesAsync(cancellationToken);
                context.ReplyLocalizedMessage("arcade_level_update", user, level);
            }
        }
        catch (Exception e)
        {
            context.ReplyLocalizedMessage("arcade_level_update_error", e.Message);
        }
    }
}