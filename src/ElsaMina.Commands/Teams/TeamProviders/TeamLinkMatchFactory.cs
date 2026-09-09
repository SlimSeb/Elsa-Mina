namespace ElsaMina.Commands.Teams.TeamProviders;

public class TeamLinkMatchFactory : ITeamLinkMatchFactory
{
    private readonly IEnumerable<ITeamProvider> _teamProviders;

    public TeamLinkMatchFactory(IEnumerable<ITeamProvider> teamProviders)
    {
        _teamProviders = teamProviders;
    }
    
    public ITeamLinkMatch FindTeamLinkMatch(string message)
    {
        return _teamProviders
            .Select(provider => GetTeamLinkMatch(message, provider))
            .FirstOrDefault(match => match != null);
    }

    private static TeamLinkMatch GetTeamLinkMatch(string message, ITeamProvider provider)
    {
        var matchingLink = provider.GetMatchFromLink(message);
        return !string.IsNullOrEmpty(matchingLink)
            ? new TeamLinkMatch(provider, matchingLink)
            : null;
    }
}