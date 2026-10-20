using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Polls.Suggestions;

[NamedCommand("banpoll")]
public class BanPollCommand : Command
{
    private const string STAFF_ROOM = "frenchstaff";

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IConfiguration _configuration;

    public BanPollCommand(IBotDbContextFactory dbContextFactory, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "banpoll_help";

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
        var existingBan = await dbContext.PollSuggestionBans
            .FirstOrDefaultAsync(ban => ban.UserId == userId && ban.RoomId == roomId, cancellationToken);

        if (existingBan != null)
        {
            context.ReplyLocalizedMessage("banpoll_already_banned", userId);
            return;
        }

        await dbContext.PollSuggestionBans.AddAsync(new PollSuggestionBan
        {
            UserId = userId,
            RoomId = roomId
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        context.ReplyLocalizedMessage("banpoll_success", userId);
        context.SendMessageIn(STAFF_ROOM,
            $"/addhtmlbox {context.GetString("banpoll_staff_message", userId, context.Sender.Name)}");
    }
}
