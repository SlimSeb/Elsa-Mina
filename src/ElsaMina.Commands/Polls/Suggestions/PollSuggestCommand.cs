using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Polls.Suggestions;

[NamedCommand("pollsuggest")]
public class PollSuggestCommand : Command
{
    private const string STAFF_ROOM = "frenchstaff";

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly IClockService _clockService;

    public PollSuggestCommand(IBotDbContextFactory dbContextFactory,
        IConfiguration configuration,
        IClockService clockService)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _clockService = clockService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "pollsuggest_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var suggestion = context.Target?.Trim();
        if (string.IsNullOrWhiteSpace(suggestion))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var roomId = _configuration.DefaultRoom;
        var userId = context.Sender.UserId;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var isBanned = await dbContext.PollSuggestionBans
            .AnyAsync(ban => ban.UserId == userId && ban.RoomId == roomId, cancellationToken);
        if (isBanned)
        {
            context.ReplyLocalizedMessage("pollsuggest_banned");
            return;
        }

        var entry = new PollSuggestion
        {
            RoomId = roomId,
            UserId = userId,
            UserName = context.Sender.Name,
            Content = suggestion,
            CreatedAt = _clockService.CurrentUtcDateTimeOffset
        };
        await dbContext.PollSuggestions.AddAsync(entry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var trigger = _configuration.Trigger;
        var allSuggestions = await dbContext.PollSuggestions
            .Where(poll => poll.RoomId == roomId)
            .OrderByDescending(poll => poll.Id)
            .ToListAsync(cancellationToken);

        var htmlBuilder = new System.Text.StringBuilder();
        htmlBuilder.Append($"<h3>{context.GetString("pollsuggest_staff_title")}</h3>");
        htmlBuilder.Append($"<p>{context.GetString("pollsuggest_staff_new", context.Sender.Name, suggestion)}</p>");

        if (allSuggestions.Count > 0)
        {
            htmlBuilder.Append($"<details><summary>{context.GetString("pollsuggest_staff_list")}</summary><ul>");
            foreach (var poll in allSuggestions)
            {
                htmlBuilder.Append($"""<li>({poll.Id}) - {poll.UserName} - {poll.Content} <button class="button" name="send" value="{trigger}deletesuggpoll {poll.Id}">Supprimer</button></li>""");
            }
            htmlBuilder.Append("</ul></details>");
        }

        context.SendMessageIn(STAFF_ROOM, $"/addhtmlbox {htmlBuilder}");
        context.ReplyLocalizedMessage("pollsuggest_success");
    }
}
