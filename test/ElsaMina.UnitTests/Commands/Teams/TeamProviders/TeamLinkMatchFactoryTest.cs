using ElsaMina.Commands.Teams.TeamProviders;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Teams.TeamProviders;

public class TeamLinkMatchFactoryTests
{
    private ITeamProvider _teamProvider1;
    private ITeamProvider _teamProvider2;
    private TeamLinkMatchFactory _factory;

    [SetUp]
    public void SetUp()
    {
        _teamProvider1 = Substitute.For<ITeamProvider>();
        _teamProvider2 = Substitute.For<ITeamProvider>();

        _factory = new TeamLinkMatchFactory([_teamProvider1, _teamProvider2]);
    }

    [Test]
    public async Task Test_FindTeamLinkMatch_ShouldReturnTeamLinkMatch_WhenATeamProviderMatchesLink()
    {
        // Arrange
        const string message = "This is a message with a valid team link";
        const string expectedLink = "https://validteamlink.com";
        _teamProvider1.GetMatchFromLink(message).ReturnsNull();
        _teamProvider2.GetMatchFromLink(message).Returns(expectedLink);

        // Act
        var result = _factory.FindTeamLinkMatch(message);

        // Assert
        await result.GetTeamExport();
        Assert.That(result, Is.Not.Null);
        await _teamProvider2.Received(1).GetTeamExport(expectedLink);
        await _teamProvider1.DidNotReceive().GetTeamExport(Arg.Any<string>());
    }

    [Test]
    public void Test_FindTeamLinkMatch_ShouldReturnNull_WhenNoTeamProviderMatchesLink()
    {
        // Arrange
        const string message = "This message has no valid team link";
        _teamProvider1.GetMatchFromLink(message).Returns((string)null);
        _teamProvider2.GetMatchFromLink(message).Returns((string)null);

        // Act
        var result = _factory.FindTeamLinkMatch(message);

        // Assert
        Assert.That(result, Is.Null);
    }
}
