namespace ElsaMina.Commands.Teams.TeamProviders;

public class TeamLinkMatch : ITeamLinkMatch
{
    private readonly ITeamProvider _provider;
    private readonly string _teamLink;

    public TeamLinkMatch(ITeamProvider provider, string teamLink)
    {
        _provider = provider;
        _teamLink = teamLink;
    }

    public async Task<SharedTeam> GetTeamExport(CancellationToken cancellationToken = default)
    {
        if (_provider == null || _teamLink == null)
        {
            return null;
        }

        return await _provider.GetTeamExport(_teamLink, cancellationToken);
    }
}