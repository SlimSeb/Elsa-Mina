using ElsaMina.Commands.Alerts;

namespace ElsaMina.UnitTests.Commands.Alerts;

public class AlertPlatformsTest
{
    [TestCase("twitch", AlertPlatforms.TWITCH)]
    [TestCase("Twitch", AlertPlatforms.TWITCH)]
    [TestCase("youtube", AlertPlatforms.YOUTUBE)]
    [TestCase("yt", AlertPlatforms.YOUTUBE)]
    [TestCase("youtubelive", AlertPlatforms.YOUTUBE_LIVE)]
    [TestCase("ytlive", AlertPlatforms.YOUTUBE_LIVE)]
    [TestCase("twitter", AlertPlatforms.TWITTER)]
    [TestCase("x", AlertPlatforms.TWITTER)]
    public void Test_TryResolve_ShouldReturnPlatform_WhenInputIsKnown(string input, string expectedPlatform)
    {
        // Act
        var isResolved = AlertPlatforms.TryResolve(input, out var platform);

        // Assert
        Assert.That(isResolved, Is.True);
        Assert.That(platform, Is.EqualTo(expectedPlatform));
    }

    [TestCase("tiktok")]
    [TestCase("")]
    [TestCase(null)]
    public void Test_TryResolve_ShouldReturnFalse_WhenInputIsUnknown(string input)
    {
        // Act
        var isResolved = AlertPlatforms.TryResolve(input, out _);

        // Assert
        Assert.That(isResolved, Is.False);
    }
}
