using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Teams.TeamProviders.Pokepaste;

public class PokepasteProvider : ITeamProvider
{
    private static readonly Regex TEAM_LINK_REGEX = new(@"https:\/\/(pokepast\.es\/[0-9A-Fa-f]{16}\/?)",
        RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private readonly IHttpService _httpService;

    public PokepasteProvider(IHttpService httpService)
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
            var response =
                await _httpService.SendAsync<PokepasteTeam>(
                    HttpRequest.Get(teamLink.Trim() + "/json"), cancellationToken);
            var pokepasteTeam = response.Data;
            return new SharedTeam
            {
                Title = pokepasteTeam.Title,
                Description = pokepasteTeam.Notes,
                Author = pokepasteTeam.Author,
                TeamExport = pokepasteTeam.Paste
            };
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while fetching a postepaste team");
            return null;
        }
    }
}