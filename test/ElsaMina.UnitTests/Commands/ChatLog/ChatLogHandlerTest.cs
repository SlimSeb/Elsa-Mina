using ElsaMina.Commands.ChatLog;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.ChatLog;

public class ChatLogHandlerTest
{
    private IChatLogService _chatLogService;
    private ChatLogHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _chatLogService = Substitute.For<IChatLogService>();
        _handler = new ChatLogHandler(_chatLogService);
    }

    [Test]
    public void Test_HandledMessageTypes_ShouldContainChatMessageType()
    {
        Assert.That(_handler.HandledMessageTypes, Contains.Item("c:"));
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAppend_WhenPartsAreValid()
    {
        var parts = new[] { "testroom", "c:", "1748476800", " SomeUser", "hello world" };

        await _handler.HandleReceivedMessageAsync(parts, "testroom");

        _chatLogService.Received(1).Append("testroom", 1748476800L, "SomeUser", "hello world");
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldTrimUsername()
    {
        var parts = new[] { "testroom", "c:", "1748476800", "  +SomeUser  ", "hi" };

        await _handler.HandleReceivedMessageAsync(parts, "testroom");

        _chatLogService.Received(1).Append("testroom", Arg.Any<long>(), "+SomeUser", "hi");
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotAppend_WhenPartsAreTooShort()
    {
        var parts = new[] { "testroom", "c:", "1748476800", " SomeUser" };

        await _handler.HandleReceivedMessageAsync(parts, "testroom");

        _chatLogService.DidNotReceiveWithAnyArgs().Append(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotAppend_WhenRoomIdIsEmpty()
    {
        var parts = new[] { "", "c:", "1748476800", " SomeUser", "hello" };

        await _handler.HandleReceivedMessageAsync(parts, string.Empty);

        _chatLogService.DidNotReceiveWithAnyArgs().Append(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotAppend_WhenRoomIdIsNull()
    {
        var parts = new[] { "", "c:", "1748476800", " SomeUser", "hello" };

        await _handler.HandleReceivedMessageAsync(parts, null);

        _chatLogService.DidNotReceiveWithAnyArgs().Append(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotAppend_WhenTimestampIsNotParseable()
    {
        var parts = new[] { "testroom", "c:", "not-a-timestamp", " SomeUser", "hello" };

        await _handler.HandleReceivedMessageAsync(parts, "testroom");

        _chatLogService.DidNotReceiveWithAnyArgs().Append(default, default, default, default);
    }
}
