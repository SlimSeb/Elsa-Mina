using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Battles.Commands;

[NamedCommand("search")]
public class SearchCommand : Command
{
    private readonly IBattleTeamsService _battleTeamsService;
    private readonly IBattleTeamSender _teamSender;

    public SearchCommand(IBattleTeamsService battleTeamsService, IBattleTeamSender teamSender)
    {
        _battleTeamsService = battleTeamsService;
        _teamSender = teamSender;
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

        _teamSender.SendWithTeam(_battleTeamsService.GetTeam(format), $"/search {format}");
        return Task.CompletedTask;
    }
}
