using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;

namespace ElsaMina.Commands.Arcade.Levels;

[NamedCommand("palier")]
public class GetArcadeLevelCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public GetArcadeLevelCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRankInPrivateMessage => Rank.Regular;
    public override string[] RoomRestriction => ["arcade", "botdevelopment"];
    public override string HelpMessageKey => "arcade_level_get_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var username = context.Target.Trim().ToLowerAlphaNum();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var arcadeLevel = await dbContext.ArcadeLevels.FindAsync([username], cancellationToken);

        if (arcadeLevel == null)
        {
            context.ReplyLocalizedMessage("arcade_level_get_not_found", username);
        }
        else
        {
            context.ReplyLocalizedMessage("arcade_level_get_success", username, arcadeLevel.Level);
        }
    }
}
