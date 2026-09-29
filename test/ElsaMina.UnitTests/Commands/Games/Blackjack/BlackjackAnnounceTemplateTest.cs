using ElsaMina.Commands.Games.Blackjack;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Games.Blackjack;

[TestFixture]
public class BlackjackAnnounceTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldWhisperJoinCommandToBot()
    {
        // Arrange
        var model = new BlackjackViewModel
        {
            Culture = Culture,
            BotName = "Bot",
            Trigger = "-",
            RoomId = "casino"
        };

        // Act
        var html = await RenderAsync("Games/Blackjack/BlackjackAnnounce", model);

        // Assert
        Assert.That(html, Does.Contain("value=\"/w Bot,-bjjoin casino\""));
        Assert.That(html, Does.Contain("bj_panel_title"));
    }
}
