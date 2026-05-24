using ElsaMina.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace ElsaMina.UnitTests.Core;

[TestFixture]
public class ShowdownSinkTest
{
    private ShowdownSink _sink;

    [SetUp]
    public void SetUp()
    {
        _sink = new ShowdownSink();
        ShowdownSink.BotSender = null;
        ShowdownSink.RoomId = null;
    }

    [TearDown]
    public void TearDown()
    {
        ShowdownSink.BotSender = null;
        ShowdownSink.RoomId = null;
    }

    private static LogEvent MakeLogEvent(LogEventLevel level, string message)
    {
        var template = new MessageTemplateParser().Parse(message);
        return new LogEvent(DateTimeOffset.UtcNow, level, null, template, []);
    }

    [Test]
    public void Test_Emit_ShouldDoNothing_WhenBotSenderIsNull()
    {
        ShowdownSink.RoomId = "botdev";
        ShowdownSink.BotSender = null;

        Assert.DoesNotThrow(() => _sink.Emit(MakeLogEvent(LogEventLevel.Warning, "test")));
    }

    [Test]
    public void Test_Emit_ShouldDoNothing_WhenRoomIdIsNull()
    {
        ShowdownSink.BotSender = (_, _) => Assert.Fail("BotSender should not be called");
        ShowdownSink.RoomId = null;

        _sink.Emit(MakeLogEvent(LogEventLevel.Warning, "test"));
    }

    [Test]
    public void Test_Emit_ShouldSendToConfiguredRoom()
    {
        var sentRoomId = string.Empty;
        var sentMessage = string.Empty;
        ShowdownSink.BotSender = (roomId, message) => { sentRoomId = roomId; sentMessage = message; };
        ShowdownSink.RoomId = "botdev";

        _sink.Emit(MakeLogEvent(LogEventLevel.Warning, "something went wrong"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sentRoomId, Is.EqualTo("botdev"));
            Assert.That(sentMessage, Does.Contain("something went wrong"));
        }
    }

    [Test]
    public void Test_Emit_ShouldIncludeLogLevel_InMessage()
    {
        var sentMessage = string.Empty;
        ShowdownSink.BotSender = (_, message) => sentMessage = message;
        ShowdownSink.RoomId = "botdev";

        _sink.Emit(MakeLogEvent(LogEventLevel.Error, "boom"));

        Assert.That(sentMessage, Does.StartWith("ERR:"));
    }

    [Test]
    public void Test_Emit_ShouldTruncateLongMessages()
    {
        var sentMessage = string.Empty;
        ShowdownSink.BotSender = (_, message) => sentMessage = message;
        ShowdownSink.RoomId = "botdev";

        _sink.Emit(MakeLogEvent(LogEventLevel.Warning, new string('x', 1000)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sentMessage.Length, Is.LessThanOrEqualTo(303));
            Assert.That(sentMessage, Does.EndWith("..."));
        }
    }
}
