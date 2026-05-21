using ElsaMina.Commands.ChatLog;

namespace ElsaMina.UnitTests.Commands.ChatLog;

public class ChatLogHelpersTest
{
    [TestCase("2026-05-21", "chatlogs/testroom/2026-05-21.txt")]
    [TestCase("2024-01-01", "chatlogs/testroom/2024-01-01.txt")]
    public void Test_GetS3Key_ShouldReturnCorrectPath(string dateStr, string expected)
    {
        var date = DateOnly.Parse(dateStr);
        Assert.That(ChatLogHelpers.GetS3Key("testroom", date), Is.EqualTo(expected));
    }

    [Test]
    public void Test_GetS3MonthPrefix_ShouldReturnCorrectPrefix()
    {
        Assert.That(ChatLogHelpers.GetS3MonthPrefix("testroom", 2026, 5),
            Is.EqualTo("chatlogs/testroom/2026-05-"));
    }

    [Test]
    public void Test_GetS3MonthPrefix_ShouldPadMonthWithLeadingZero()
    {
        Assert.That(ChatLogHelpers.GetS3MonthPrefix("testroom", 2026, 1),
            Is.EqualTo("chatlogs/testroom/2026-01-"));
    }

    [Test]
    public void Test_TryParseLine_ShouldReturnTrue_WhenLineIsValid()
    {
        const string line = "[12:34:56 UTC] someuser: hello world";

        var result = ChatLogHelpers.TryParseLine(line, out var username, out var message);

        Assert.That(result, Is.True);
        Assert.That(username, Is.EqualTo("someuser"));
        Assert.That(message, Is.EqualTo("hello world"));
    }

    [Test]
    public void Test_TryParseLine_ShouldReturnTrue_WhenMessageContainsColon()
    {
        const string line = "[12:34:56 UTC] someuser: hello: world";

        var result = ChatLogHelpers.TryParseLine(line, out var username, out var message);

        Assert.That(result, Is.True);
        Assert.That(username, Is.EqualTo("someuser"));
        Assert.That(message, Is.EqualTo("hello: world"));
    }

    [Test]
    public void Test_TryParseLine_ShouldReturnTrue_WhenUsernameHasRankPrefix()
    {
        const string line = "[12:34:56 UTC] +someuser: hello";

        var result = ChatLogHelpers.TryParseLine(line, out var username, out var message);

        Assert.That(result, Is.True);
        Assert.That(username, Is.EqualTo("+someuser"));
        Assert.That(message, Is.EqualTo("hello"));
    }

    [Test]
    public void Test_TryParseLine_ShouldReturnFalse_WhenLineIsTooShort()
    {
        var result = ChatLogHelpers.TryParseLine("[12:34:56", out _, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Test_TryParseLine_ShouldReturnFalse_WhenLineDoesNotStartWithBracket()
    {
        const string line = "12:34:56 UTC] someuser: hello";

        var result = ChatLogHelpers.TryParseLine(line, out _, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Test_TryParseLine_ShouldReturnFalse_WhenNoSeparatorFound()
    {
        const string line = "[12:34:56 UTC] someuser without separator";

        var result = ChatLogHelpers.TryParseLine(line, out _, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Test_TryParseLine_ShouldReturnFalse_WhenLineIsEmpty()
    {
        var result = ChatLogHelpers.TryParseLine(string.Empty, out _, out _);

        Assert.That(result, Is.False);
    }
}
