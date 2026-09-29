using ElsaMina.Commands.Alerts.Twitch;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Alerts.Twitch;

[TestFixture]
public class TwitchLiveAlertTemplateTest : TemplateTestBase
{
    private static TwitchLiveAlertViewModel CreateModel(string thumbnailUrl = null, string gameName = null) => new()
    {
        Culture = Culture,
        ChannelLogin = "streamer",
        ChannelDisplayName = "Streamer",
        Title = "Ranked ladder",
        ThumbnailUrl = thumbnailUrl,
        GameName = gameName
    };

    [Test]
    public async Task Test_Render_ShouldShowChannelAndTitle()
    {
        // Arrange
        SetResourceString("alerts_twitch_live", "{0} is live!");

        // Act
        var html = await RenderAsync("Alerts/Twitch/TwitchLiveAlert", CreateModel());

        // Assert
        Assert.That(html, Does.Contain("Streamer is live!"));
        Assert.That(html, Does.Contain("<span>Ranked ladder</span>"));
        Assert.That(html, Does.Contain("href=\"https://www.twitch.tv/streamer\""));
    }

    [Test]
    public async Task Test_Render_ShouldShowThumbnail_WhenThumbnailUrlIsSet()
    {
        // Act
        var html = await RenderAsync("Alerts/Twitch/TwitchLiveAlert",
            CreateModel(thumbnailUrl: "https://example.com/thumb.jpg"));

        // Assert
        Assert.That(html, Does.Contain("src=\"https://example.com/thumb.jpg\""));
    }

    [Test]
    public async Task Test_Render_ShouldNotShowThumbnail_WhenThumbnailUrlIsEmpty()
    {
        // Act
        var html = await RenderAsync("Alerts/Twitch/TwitchLiveAlert", CreateModel(thumbnailUrl: string.Empty));

        // Assert
        Assert.That(html, Does.Not.Contain("<img"));
    }

    [Test]
    public async Task Test_Render_ShouldShowGame_WhenGameNameIsSet()
    {
        // Arrange
        SetResourceString("alerts_twitch_playing", "Playing {0}");

        // Act
        var html = await RenderAsync("Alerts/Twitch/TwitchLiveAlert", CreateModel(gameName: "Pokemon Scarlet"));

        // Assert
        Assert.That(html, Does.Contain("Playing Pokemon Scarlet"));
    }

    [Test]
    public async Task Test_Render_ShouldNotShowGame_WhenGameNameIsMissing()
    {
        // Act
        var html = await RenderAsync("Alerts/Twitch/TwitchLiveAlert", CreateModel());

        // Assert
        Assert.That(html, Does.Not.Contain("alerts_twitch_playing"));
    }
}
