using ElsaMina.Commands.Alerts;

namespace ElsaMina.UnitTests.Commands.Alerts;

public class AlertArgumentsTest
{
    [TestCase("twitch someone", "twitch", "someone")]
    [TestCase("twitch, someone", "twitch", "someone")]
    [TestCase("  youtube   UCabcDEF  ", "youtube", "UCabcDEF")]
    public void Test_TryParse_ShouldSplitPlatformAndChannel_WhenTwoArgumentsAreGiven(string target,
        string expectedPlatform, string expectedChannel)
    {
        // Act
        var isParsed = AlertArguments.TryParse(target, out var platform, out var channel);

        // Assert
        Assert.That(isParsed, Is.True);
        Assert.That(platform, Is.EqualTo(expectedPlatform));
        Assert.That(channel, Is.EqualTo(expectedChannel));
    }

    [TestCase("twitch")]
    [TestCase("twitch a b")]
    [TestCase("")]
    [TestCase(null)]
    public void Test_TryParse_ShouldReturnFalse_WhenArgumentCountIsWrong(string target)
    {
        // Act
        var isParsed = AlertArguments.TryParse(target, out _, out _);

        // Assert
        Assert.That(isParsed, Is.False);
    }
}
