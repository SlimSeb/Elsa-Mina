using ElsaMina.Commands.Teams;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Newtonsoft.Json;

namespace ElsaMina.Battles.Commands;

[NamedCommand("useteam")]
public class UseTeamCommand : Command
{
    private readonly IBotDbContextFactory _botDbContextFactory;
    private readonly IBattleTeamsService _battleTeamsService;

    public UseTeamCommand(IBotDbContextFactory botDbContextFactory, IBattleTeamsService battleTeamsService)
    {
        _botDbContextFactory = botDbContextFactory;
        _battleTeamsService = battleTeamsService;
    }

    public override bool IsWhitelistOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target?.Split(',');
        if (parts is not { Length: 2 })
        {
            context.Reply("Usage: useteam <team name>, <format>");
            return;
        }

        var teamName = parts[0].Trim();
        var format = parts[1].Trim();
        if (string.IsNullOrWhiteSpace(teamName) || string.IsNullOrWhiteSpace(format))
        {
            context.Reply("Usage: useteam <team name>, <format>");
            return;
        }

        await using var dbContext = await _botDbContextFactory.CreateDbContextAsync(cancellationToken);
        var team = await dbContext.Teams.FindAsync([teamName.ToLowerAlphaNum()], cancellationToken);
        if (team == null)
        {
            context.Reply($"No team named \"{teamName}\" was found.");
            return;
        }

        var sets = JsonConvert.DeserializeObject<List<PokemonSet>>(team.TeamJson);
        var packedTeam = ShowdownTeamsUtils.PackTeam(sets);
        if (string.IsNullOrEmpty(packedTeam))
        {
            context.Reply($"The team \"{teamName}\" could not be packed.");
            return;
        }

        _battleTeamsService.SetTeam(format, packedTeam);
        context.Reply($"Team \"{team.Name}\" is now used for searches in format {format}.");
    }
}
