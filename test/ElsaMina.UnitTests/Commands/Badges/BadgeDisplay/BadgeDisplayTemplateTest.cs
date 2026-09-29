using ElsaMina.Commands.Badges.BadgeDisplay;
using ElsaMina.DataAccess.Models;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Badges.BadgeDisplay;

[TestFixture]
public class BadgeDisplayTemplateTest : TemplateTestBase
{
    private static BadgeDisplayViewModel CreateModel(Badge badge, params string[] holders) => new()
    {
        Culture = Culture,
        DisplayedBadge = badge,
        BadgeHolders = holders
    };

    [Test]
    public async Task Test_Render_ShouldShowBadgeImageNameAndHolders()
    {
        // Arrange
        var badge = new Badge { Name = "Champion", Image = "https://example.com/badge.png" };

        // Act
        var html = await RenderAsync("Badges/BadgeDisplay/BadgeDisplay", CreateModel(badge, "alice", "bob"));

        // Assert
        Assert.That(html, Does.Contain("src=\"https://example.com/badge.png\""));
        Assert.That(html, Does.Contain("<b>Champion</b>"));
        Assert.That(html, Does.Contain("alice, bob"));
        Assert.That(html, Does.Not.Contain("badge_display_no_holders"));
    }

    [Test]
    public async Task Test_Render_ShouldShowNoHoldersMessage_WhenNobodyHoldsTheBadge()
    {
        // Arrange
        var badge = new Badge { Name = "Champion", Image = "https://example.com/badge.png" };

        // Act
        var html = await RenderAsync("Badges/BadgeDisplay/BadgeDisplay", CreateModel(badge));

        // Assert
        Assert.That(html, Does.Contain("badge_display_no_holders"));
    }

    [TestCase(true, false, "badge_display_is_trophy", "badge_display_is_team_tournament")]
    [TestCase(false, true, "badge_display_is_team_tournament", "badge_display_is_trophy")]
    public async Task Test_Render_ShouldShowBadgeKind_WhenBadgeIsTrophyOrTeamTournament(bool isTrophy,
        bool isTeamTournament, string expectedKey, string unexpectedKey)
    {
        // Arrange
        var badge = new Badge
        {
            Name = "Champion",
            Image = "https://example.com/badge.png",
            IsTrophy = isTrophy,
            IsTeamTournament = isTeamTournament
        };

        // Act
        var html = await RenderAsync("Badges/BadgeDisplay/BadgeDisplay", CreateModel(badge, "alice"));

        // Assert
        Assert.That(html, Does.Contain(expectedKey));
        Assert.That(html, Does.Not.Contain(unexpectedKey));
    }
}
