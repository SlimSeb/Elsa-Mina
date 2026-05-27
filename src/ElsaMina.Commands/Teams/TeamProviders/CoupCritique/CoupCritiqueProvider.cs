using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Teams.TeamProviders.CoupCritique;

public class CoupCritiqueProvider : ITeamProvider
{
    private const string COUP_CRITIQUE_API_URL = "https://www.coupcritique.fr/api/teams/{0}";

    private static readonly Regex TEAM_LINK_REGEX = new(@"https:\/\/(www\.coupcritique\.fr\/entity\/teams\/\d+\/?)",
        RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private readonly IHttpService _httpService;

    public CoupCritiqueProvider(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public string GetMatchFromLink(string teamLink)
    {
        var match = TEAM_LINK_REGEX.Match(teamLink);
        return match.Success ? match.Value : null;
    }

    public async Task<SharedTeam> GetTeamExport(string teamLink, CancellationToken cancellationToken = default)
    {
        try
        {
            if (teamLink.Last() == '/')
            {
                teamLink = teamLink.Remove(teamLink.Length - 1);
            }

            var urlParts = teamLink.Split('/');
            var teamId = urlParts[^1];
            var response =
                await _httpService.SendAsync<CoupCritiqueResponse>(
                    HttpRequest.Get(string.Format(COUP_CRITIQUE_API_URL, teamId)), cancellationToken);
            var team = response.Data;
            return new SharedTeam
            {
                Description = team.Team.Description,
                TeamExport = team.Team.Export,
                Author = team.Team.User.UserName,
                Title = team.Team.Name
            };
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while fetching team from Coup Critique");
            return null;
        }
    }
}