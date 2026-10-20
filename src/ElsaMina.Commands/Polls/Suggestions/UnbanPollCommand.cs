using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Polls.Suggestions;

[NamedCommand("unbanpoll")]
public class UnbanPollCommand : Command
{
    private const string STAFF_ROOM = "frenchstaff";

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IConfiguration _configuration;

    public UnbanPollCommand(IBotDbContextFactory dbContextFactory, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "unbanpoll_help";

    // Les suggestions de sondages sont celles de la room par défaut, même quand on lance la commande ailleurs
    public override string GetTargetRoomId(IContext context) => _configuration.DefaultRoom;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var userId = context.Target.Trim().ToLowerAlphaNum();
        var roomId = GetTargetRoomId(context);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var ban = await dbContext.PollSuggestionBans
            .FirstOrDefaultAsync(ban => ban.UserId == userId && ban.RoomId == roomId, cancellationToken);

        if (ban == null)
        {
            context.ReplyLocalizedMessage("unbanpoll_not_banned", userId);
            return;
        }

        dbContext.PollSuggestionBans.Remove(ban);
        await dbContext.SaveChangesAsync(cancellationToken);

        context.ReplyLocalizedMessage("unbanpoll_success", userId);
        context.SendMessageIn(STAFF_ROOM,
            $"/addhtmlbox {context.GetString("unbanpoll_staff_message", userId, context.Sender.Name)}");
    }
}
