using ElsaMina.Commands.Alerts.Twitter;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Alerts.Twitter;

[TestFixture]
public class TweetAlertTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldLinkToTweet_WhenModelIsComplete()
    {
        // Arrange
        SetResourceString("alerts_tweet", "New tweet from {0}");
        var model = new TweetAlertViewModel
        {
            Culture = Culture,
            Username = "pokemon",
            TweetId = "123456",
            Text = "Hello trainers"
        };

        // Act
        var html = await RenderAsync("Alerts/Twitter/TweetAlert", model);

        // Assert
        Assert.That(html, Does.Contain("New tweet from pokemon"));
        Assert.That(html, Does.Contain("<span>Hello trainers</span>"));
        Assert.That(html, Does.Contain("href=\"https://x.com/pokemon/status/123456\""));
    }

    [Test]
    public async Task Test_Render_ShouldEncodeTweetText()
    {
        // Arrange
        var model = new TweetAlertViewModel
        {
            Culture = Culture,
            Username = "pokemon",
            TweetId = "1",
            Text = "<script>alert(1)</script>"
        };

        // Act
        var html = await RenderAsync("Alerts/Twitter/TweetAlert", model);

        // Assert
        Assert.That(html, Does.Not.Contain("<script>"));
        Assert.That(html, Does.Contain("&lt;script&gt;"));
    }
}
