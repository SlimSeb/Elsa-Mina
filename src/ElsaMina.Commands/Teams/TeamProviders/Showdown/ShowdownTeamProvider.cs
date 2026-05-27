using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Teams.TeamProviders.Showdown;

public class ShowdownTeamProvider : ITeamProvider
{
    private const string SHOWDOWN_TEAM_API_URL = "https://teams.pokemonshowdown.com/api/getteam?teamid={0}&full=1";

    private const string SHOWDOWN_TEAM_API_URL_WITH_PASSWORD =
        "https://teams.pokemonshowdown.com/api/getteam?teamid={0}&password={1}&full=1";

    private static readonly Regex SHOWDOWN_TEAM_LINK_REGEX =
        new(@"https://(psim\.us/t|teams\.pokemonshowdown\.com/view)/\d+(-[a-z0-9]+)?", RegexOptions.Compiled,
            Constants.REGEX_MATCH_TIMEOUT);

    private readonly IHttpService _httpService;

    public ShowdownTeamProvider(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public string GetMatchFromLink(string teamLink)
    {
        var match = SHOWDOWN_TEAM_LINK_REGEX.Match(teamLink);
        return match.Success ? match.Value : null;
    }

    public async Task<SharedTeam> GetTeamExport(string teamLink, CancellationToken cancellationToken = default)
    {
        var slug = teamLink.Split("/")[^1];
        var dashIndex = slug.IndexOf('-');
        var teamId = dashIndex >= 0 ? slug.Substring(0, dashIndex) : slug;
        var password = dashIndex >= 0 ? slug.Substring(dashIndex + 1) : null;
        try
        {
            var url = password != null
                ? string.Format(SHOWDOWN_TEAM_API_URL_WITH_PASSWORD, teamId, password)
                : string.Format(SHOWDOWN_TEAM_API_URL, teamId);
            var result = await _httpService.SendAsync<ShowdownTeamDto>(
                HttpRequest.Get(url).SkippingFirstResponseCharacter(), cancellationToken);
            return new SharedTeam
            {
                Author = result.Data.OwnerId,
                Description = string.Empty,
                Title = result.Data.Title,
                TeamExport = ShowdownTeamsUtils.GetTeamExport(ShowdownTeamsUtils.UnpackTeam(result.Data.PackedTeam))
            };
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while fetching team from Showdown");
            return null;
        }
    }
}