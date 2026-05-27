using System.Net;
using ElsaMina.Commands.Teams.TeamProviders.Pokepaste;
using ElsaMina.Core.Services.Http;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Teams.TeamProviders.Pokepaste;

public class PokepasteProviderTests
{
    private IHttpService _httpServiceMock;
    private PokepasteProvider _provider;

    [SetUp]
    public void SetUp()
    {
        _httpServiceMock = Substitute.For<IHttpService>();
        _provider = new PokepasteProvider(_httpServiceMock);
    }

    [Test]
    public void GetMatchFromLink_ShouldReturnMatch_WhenLinkIsValid()
    {
        // Arrange
        var validTeamLink = "https://pokepast.es/1234abcd5678efff";
        
        // Act
        var result = _provider.GetMatchFromLink(validTeamLink);
        
        // Assert
        Assert.That(result, Is.EqualTo(validTeamLink));
    }

    [Test]
    public void GetMatchFromLink_ShouldReturnNull_WhenLinkIsInvalid()
    {
        // Arrange
        var invalidTeamLink = "https://invalidsite.com/teams/12345";
        
        // Act
        var result = _provider.GetMatchFromLink(invalidTeamLink);
        
        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetTeamExport_ShouldReturnSharedTeam_WhenHttpResponseIsSuccessful()
    {
        // Arrange
        var teamLink = "https://pokepast.es/1234abcd5678efgh";
        var apiUrl = teamLink.Trim() + "/json";
        var expectedPokepasteTeam = new PokepasteTeam
        {
            Title = "Test Team",
            Notes = "This is a test team",
            Author = "TestUser",
            Paste = "Team export data"
        };

        var expectedResponse = new HttpResponse<PokepasteTeam>
        {
            StatusCode = HttpStatusCode.OK,
            Data = expectedPokepasteTeam
        };

        _httpServiceMock.SendAsync<PokepasteTeam>(Arg.Is<HttpRequest>(request => request.Uri == apiUrl))
            .Returns(expectedResponse);

        // Act
        var result = await _provider.GetTeamExport(teamLink);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Title, Is.EqualTo(expectedPokepasteTeam.Title));
            Assert.That(result.Description, Is.EqualTo(expectedPokepasteTeam.Notes));
            Assert.That(result.Author, Is.EqualTo(expectedPokepasteTeam.Author));
            Assert.That(result.TeamExport, Is.EqualTo(expectedPokepasteTeam.Paste));
        }
    }

    [Test]
    public async Task GetTeamExport_ShouldReturnNull_WhenHttpRequestFails()
    {
        // Arrange
        const string teamLink = "https://pokepast.es/1234abcd5678efgh";
        var apiUrl = teamLink.Trim() + "/json";
        _httpServiceMock
            .SendAsync<PokepasteTeam>(Arg.Is<HttpRequest>(request => request.Uri == apiUrl))
            .Returns<Task<IHttpResponse<PokepasteTeam>>>(_ => throw new Exception("Network error"));

        // Act
        var result = await _provider.GetTeamExport(teamLink);

        // Assert
        Assert.That(result, Is.Null);
    }
}