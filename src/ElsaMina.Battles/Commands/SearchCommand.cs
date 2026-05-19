using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Battles.Commands;

[NamedCommand("search")]
public class SearchCommand : Command
{
    public override bool IsWhitelistOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var format = context.Target?.Trim();
        if (string.IsNullOrWhiteSpace(format))
        {
            context.Reply("Usage: /search <format>");
            return Task.CompletedTask;
        }

        context.SendMessageIn(context.RoomId, $"/search {format}");
        return Task.CompletedTask;
    }
}