using ElsaMina.Commands.ChatLog;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.ChatLog;

[TestFixture]
public class TopUsersTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldShowTitleAndOneRowPerUser()
    {
        // Arrange
        SetResourceString("topusers_title", "Top users of {0}");
        var model = new TopUsersViewModel
        {
            Culture = Culture,
            RoomId = "lobby",
            MaxCount = 50,
            Users =
            [
                new TopUsersRow { UserId = "alice", Count = 50 },
                new TopUsersRow { UserId = "bob", Count = 20 }
            ]
        };

        // Act
        var html = await RenderAsync("ChatLog/TopUsers", model);

        // Assert
        Assert.That(html, Does.Contain("Top users of lobby"));
        Assert.That(html, Does.Contain("<small>alice</small>"));
        Assert.That(html, Does.Contain("<small>bob</small>"));
        Assert.That(html, Does.Contain("max=\"50\" value=\"50\""));
        Assert.That(html, Does.Contain("max=\"50\" value=\"20\""));
        Assert.That(html.IndexOf("alice", StringComparison.Ordinal),
            Is.LessThan(html.IndexOf("bob", StringComparison.Ordinal)));
    }
}
