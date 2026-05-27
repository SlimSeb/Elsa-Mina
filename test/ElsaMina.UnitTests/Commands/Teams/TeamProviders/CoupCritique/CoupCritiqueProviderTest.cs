using System.Net;
using ElsaMina.Commands.Teams.TeamProviders.CoupCritique;
using ElsaMina.Core.Services.Http;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Teams.TeamProviders.CoupCritique;

public class CoupCritiqueProviderTest
{
    private IHttpService _httpService;
    private CoupCritiqueProvider _provider;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _provider = new CoupCritiqueProvider(_httpService);
    }

    [Test]
    public void GetMatchFromLink_ShouldReturnMatch_WhenLinkIsValid()
    {
        // Arrange
        const string validTeamLink = "https://www.coupcritique.fr/entity/teams/12345";

        // Act
        var result = _provider.GetMatchFromLink(validTeamLink);

        // Assert
        Assert.That(result, Is.EqualTo(validTeamLink));
    }

    [Test]
    public void GetMatchFromLink_ShouldReturnNull_WhenLinkIsInvalid()
    {
        // Arrange
        const string invalidTeamLink = "https://invalidsite.com/teams/12345";

        // Act
        var result = _provider.GetMatchFromLink(invalidTeamLink);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetTeamExport_ShouldReturnSharedTeam_WhenHttpResponseIsSuccessful()
    {
        // Arrange
        const string teamLink = "https://www.coupcritique.fr/entity/teams/12345";
        const string teamId = "12345";
        const string apiUrl = $"https://www.coupcritique.fr/api/teams/{teamId}";
        var expectedTeamData = new CoupCritiqueResponse
        {
            Team = new CoupCritiqueTeam
            {
                Name = "Example Team",
                Description = "A test team",
                Export = "Export data",
                User = new CoupCritiqueUser { UserName = "TestUser" }
            }
        };

        var expectedResponse = new HttpResponse<CoupCritiqueResponse>
        {
            StatusCode = HttpStatusCode.OK,
            Data = expectedTeamData
        };

        _httpService.SendAsync<CoupCritiqueResponse>(Arg.Is<HttpRequest>(request => request.Uri == apiUrl))
            .Returns(expectedResponse);

        // Act
        var result = await _provider.GetTeamExport(teamLink);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Title, Is.EqualTo(expectedTeamData.Team.Name));
            Assert.That(result.Description, Is.EqualTo(expectedTeamData.Team.Description));
            Assert.That(result.TeamExport, Is.EqualTo(expectedTeamData.Team.Export));
            Assert.That(result.Author, Is.EqualTo(expectedTeamData.Team.User.UserName));
        }
    }

    [Test]
    public async Task GetTeamExport_ShouldReturnNull_WhenHttpRequestFails()
    {
        // Arrange
        const string teamLink = "https://www.coupcritique.fr/entity/teams/12345";
        const string teamId = "12345";
        const string apiUrl = $"https://www.coupcritique.fr/api/teams/{teamId}";
        _httpService
            .SendAsync<CoupCritiqueResponse>(Arg.Is<HttpRequest>(request => request.Uri == apiUrl))
            .Returns<Task<IHttpResponse<CoupCritiqueResponse>>>(_ => throw new Exception("Network error"));

        // Act
        var result = await _provider.GetTeamExport(teamLink);

        // Assert
        Assert.That(result, Is.Null);
    }
}