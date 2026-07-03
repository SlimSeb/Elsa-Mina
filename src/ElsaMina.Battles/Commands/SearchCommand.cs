using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Battles.Commands;

[NamedCommand("search")]
public class SearchCommand : Command
{
    private readonly IBattleTeamsService _battleTeamsService;

    public SearchCommand(IBattleTeamsService battleTeamsService)
    {
        _battleTeamsService = battleTeamsService;
    }

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

        var packedTeam = _battleTeamsService.GetTeam(format);
        if (!string.IsNullOrEmpty(packedTeam))
        {
            context.SendMessageIn(string.Empty, $"/utm {packedTeam}");
        }

        context.SendMessageIn(string.Empty, $"/search {format}");
        return Task.CompletedTask;
    }
}
