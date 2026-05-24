using System.Net;
using ElsaMina.Commands.Teams.TeamProviders.Showdown;
using ElsaMina.Core.Services.Http;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Teams.TeamProviders.Showdown;

public class ShowdownTeamProviderTest
{
    private const string PACKED_TEAM =
        "Metagross||FocusSash|clearbody|MeteorMash,Earthquake,BulletPunch,Explosion|adamant|80,252,,,,176|||||";

    private IHttpService _httpServiceMock;
    private ShowdownTeamProvider _provider;

    [SetUp]
    public void SetUp()
    {
        _httpServiceMock = Substitute.For<IHttpService>();
        _provider = new ShowdownTeamProvider(_httpServiceMock);
    }

    [TestCase("https://psim.us/t/1718380")]
    [TestCase("https://psim.us/t/1718725-sq25g7s6nro5eivvbkdr")]
    [TestCase("https://teams.pokemonshowdown.com/view/191192")]
    [TestCase("https://teams.pokemonshowdown.com/view/191192-wipnfvjsorz0vlpst358")]
    public void Test_GetMatchFromLink_ShouldReturnMatch_WhenLinkIsValid(string validLink)
    {
        var result = _provider.GetMatchFromLink(validLink);

        Assert.That(result, Is.EqualTo(validLink));
    }

    [TestCase("https://invalidsite.com/teams/12345")]
    [TestCase("https://pokepast.es/1234abcd")]
    [TestCase("not a url")]
    public void Test_GetMatchFromLink_ShouldReturnNull_WhenLinkIsInvalid(string invalidLink)
    {
        var result = _provider.GetMatchFromLink(invalidLink);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetTeamExport_ShouldCallApiWithoutPassword_WhenShortLinkHasNoPassword()
    {
        var teamLink = "https://psim.us/t/1718380";
        var expectedUrl = "https://teams.pokemonshowdown.com/api/getteam?teamid=1718380&full=1";
        SetUpHttpMock(expectedUrl, "owner", "My Team", PACKED_TEAM);

        await _provider.GetTeamExport(teamLink);

        await _httpServiceMock.Received(1).GetAsync<ShowdownTeamDto>(expectedUrl,
            removeFirstCharacterFromResponse: true, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetTeamExport_ShouldCallApiWithPassword_WhenShortLinkHasPassword()
    {
        var teamLink = "https://psim.us/t/1718725-sq25g7s6nro5eivvbkdr";
        var expectedUrl = "https://teams.pokemonshowdown.com/api/getteam?teamid=1718725&password=sq25g7s6nro5eivvbkdr&full=1";
        SetUpHttpMock(expectedUrl, "owner", "My Team", PACKED_TEAM);

        await _provider.GetTeamExport(teamLink);

        await _httpServiceMock.Received(1).GetAsync<ShowdownTeamDto>(expectedUrl,
            removeFirstCharacterFromResponse: true, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetTeamExport_ShouldCallApiWithoutPassword_WhenLongLinkHasNoPassword()
    {
        var teamLink = "https://teams.pokemonshowdown.com/view/191192";
        var expectedUrl = "https://teams.pokemonshowdown.com/api/getteam?teamid=191192&full=1";
        SetUpHttpMock(expectedUrl, "owner", "My Team", PACKED_TEAM);

        await _provider.GetTeamExport(teamLink);

        await _httpServiceMock.Received(1).GetAsync<ShowdownTeamDto>(expectedUrl,
            removeFirstCharacterFromResponse: true, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetTeamExport_ShouldCallApiWithPassword_WhenLongLinkHasPassword()
    {
        var teamLink = "https://teams.pokemonshowdown.com/view/191192-wipnfvjsorz0vlpst358";
        var expectedUrl = "https://teams.pokemonshowdown.com/api/getteam?teamid=191192&password=wipnfvjsorz0vlpst358&full=1";
        SetUpHttpMock(expectedUrl, "owner", "My Team", PACKED_TEAM);

        await _provider.GetTeamExport(teamLink);

        await _httpServiceMock.Received(1).GetAsync<ShowdownTeamDto>(expectedUrl,
            removeFirstCharacterFromResponse: true, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetTeamExport_ShouldReturnSharedTeam_WithCorrectFields()
    {
        var teamLink = "https://psim.us/t/1718380";
        var expectedUrl = "https://teams.pokemonshowdown.com/api/getteam?teamid=1718380&full=1";
        SetUpHttpMock(expectedUrl, "panure", "Untitled 21", PACKED_TEAM);

        var result = await _provider.GetTeamExport(teamLink);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Author, Is.EqualTo("panure"));
            Assert.That(result.Title, Is.EqualTo("Untitled 21"));
            Assert.That(result.Description, Is.EqualTo(string.Empty));
            Assert.That(result.TeamExport, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public async Task Test_GetTeamExport_ShouldReturnNull_WhenHttpRequestFails()
    {
        var teamLink = "https://psim.us/t/1718380";
        var expectedUrl = "https://teams.pokemonshowdown.com/api/getteam?teamid=1718380&full=1";
        _httpServiceMock
            .GetAsync<ShowdownTeamDto>(expectedUrl, removeFirstCharacterFromResponse: true,
                cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Network error"));

        var result = await _provider.GetTeamExport(teamLink);

        Assert.That(result, Is.Null);
    }

    private void SetUpHttpMock(string url, string ownerId, string title, string packedTeam)
    {
        _httpServiceMock
            .GetAsync<ShowdownTeamDto>(url, removeFirstCharacterFromResponse: true,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<ShowdownTeamDto>
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ShowdownTeamDto
                {
                    OwnerId = ownerId,
                    Title = title,
                    PackedTeam = packedTeam
                }
            });
    }
}
