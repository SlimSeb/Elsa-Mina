using ElsaMina.Commands.Badges.BadgeHolders;
using ElsaMina.DataAccess.Models;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Badges.BadgeHolders;

[TestFixture]
public class BadgeHoldersTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldShowEveryBadgeWithItsHolders()
    {
        // Arrange
        var champion = new Badge
        {
            Name = "Champion",
            Image = "https://example.com/champion.png",
            BadgeHolders =
            [
                new BadgeHolding { UserId = "alice", RoomUser = new RoomUser { User = new SavedUser { UserName = "Alice" } } }
            ]
        };
        var finalist = new Badge { Name = "Finalist", Image = "https://example.com/finalist.png" };
        var model = new BadgeHoldersViewModel { Culture = Culture, Badges = [champion, finalist] };

        // Act
        var html = await RenderAsync("Badges/BadgeHolders/BadgeHolders", model);

        // Assert
        Assert.That(html, Does.Contain("<b>Champion</b>"));
        Assert.That(html, Does.Contain("<b>Finalist</b>"));
        Assert.That(html, Does.Contain("Alice"));
        Assert.That(html, Does.Contain("badge_display_no_holders"));
    }

    [Test]
    public async Task Test_Render_ShouldFallBackToUserId_WhenHolderHasNoSavedUserName()
    {
        // Arrange
        var badge = new Badge
        {
            Name = "Champion",
            Image = "https://example.com/champion.png",
            BadgeHolders = [new BadgeHolding { UserId = "alice" }]
        };
        var model = new BadgeHoldersViewModel { Culture = Culture, Badges = [badge] };

        // Act
        var html = await RenderAsync("Badges/BadgeHolders/BadgeHolders", model);

        // Assert
        Assert.That(html, Does.Contain("alice"));
    }
}
