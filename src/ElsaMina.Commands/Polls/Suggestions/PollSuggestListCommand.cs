using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Polls.Suggestions;

[NamedCommand("pollsuggestlist", Aliases = ["suggpolllist"])]
public class PollSuggestListCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IConfiguration _configuration;

    public PollSuggestListCommand(IBotDbContextFactory dbContextFactory, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Driver;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = _configuration.DefaultRoom;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var suggestions = await dbContext.PollSuggestions
            .Where(poll => poll.RoomId == roomId)
            .OrderByDescending(poll => poll.Id)
            .ToListAsync(cancellationToken);

        if (suggestions.Count == 0)
        {
            context.ReplyLocalizedMessage("pollsuggestlist_empty");
            return;
        }

        var trigger = _configuration.Trigger;
        var htmlBuilder = new System.Text.StringBuilder();
        htmlBuilder.Append($"<h3>{context.GetString("pollsuggest_staff_list")}</h3><ul>");
        foreach (var poll in suggestions)
        {
            htmlBuilder.Append($"""<li>({poll.Id}) - {poll.UserName} - {poll.Content} <button class="button" name="send" value="{trigger}deletesuggpoll {poll.Id}">Supprimer</button></li>""");
        }
        htmlBuilder.Append("</ul>");

        context.ReplyHtml(htmlBuilder.ToString());
    }
}
