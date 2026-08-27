using ElsaMina.Commands.Ai.Chat;
using ElsaMina.Core.Services.LanguageModel;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Ai.Chat;

public class ConversationHistoryServiceTest
{
    private IConfiguration _configuration;
    private ConversationHistoryService _service;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");
        _service = new ConversationHistoryService(_configuration);
    }

    private static IRoom RoomWithMessages(params (string user, string message)[] messages)
    {
        var room = Substitute.For<IRoom>();
        room.LastMessages.Returns(messages.Select(entry => Tuple.Create(entry.user, entry.message)));
        return room;
    }

    private static IUser UserNamed(string name)
    {
        var user = Substitute.For<IUser>();
        user.Name.Returns(name);
        return user;
    }

    [Test]
    public void Test_BuildConversation_ShouldSkipCommandAndEmptyMessages()
    {
        // Arrange
        var room = RoomWithMessages(
            ("Carl", "   "),
            ("Bob", "-ping"),
            ("elsamina", "hi there"),
            ("+Alice", "hello"));

        // Act
        var conversation = _service.BuildConversation(room, UserNamed("Dave"), "how are you");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            // LastMessages is iterated in reverse, so +Alice comes before the bot line.
            Assert.That(conversation, Has.Count.EqualTo(3));
            Assert.That(conversation[0].Role, Is.EqualTo(MessageRole.User));
            Assert.That(conversation[0].Content, Is.EqualTo("Alice: hello"));
            Assert.That(conversation[1].Role, Is.EqualTo(MessageRole.Agent));
            Assert.That(conversation[1].Content, Is.EqualTo("hi there"));
            Assert.That(conversation[2].Content, Is.EqualTo("Dave: how are you"));
        }
    }

    [Test]
    public void Test_BuildConversation_ShouldTagBotMessagesAsAgent()
    {
        // Arrange
        var room = RoomWithMessages(("ElsaMina", "I am the bot"));

        // Act
        var conversation = _service.BuildConversation(room, UserNamed("Dave"), string.Empty);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversation, Has.Count.EqualTo(1));
            Assert.That(conversation[0].Role, Is.EqualTo(MessageRole.Agent));
            Assert.That(conversation[0].Content, Is.EqualTo("I am the bot"));
        }
    }

    [Test]
    public void Test_BuildConversation_ShouldStripRankSymbolFromUserName()
    {
        // Arrange
        var room = RoomWithMessages(("%Moderator", "message"));

        // Act
        var conversation = _service.BuildConversation(room, UserNamed("Dave"), string.Empty);

        // Assert
        Assert.That(conversation[0].Content, Is.EqualTo("Moderator: message"));
    }

    [Test]
    public void Test_BuildConversation_ShouldNotAppendLatestMessage_WhenBlank()
    {
        // Arrange
        var room = RoomWithMessages(("Alice", "hello"));

        // Act
        var conversation = _service.BuildConversation(room, UserNamed("Dave"), "   ");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversation, Has.Count.EqualTo(1));
            Assert.That(conversation[0].Content, Is.EqualTo("Alice: hello"));
        }
    }

    [Test]
    public void Test_BuildConversation_ShouldTrimHistory_WhenExceedingMaximum()
    {
        // Arrange
        var messages = Enumerable.Range(0, 30)
            .Select(index => ("Alice", $"message {index}"))
            .ToArray();
        var room = RoomWithMessages(messages);

        // Act
        var conversation = _service.BuildConversation(room, UserNamed("Dave"), "latest");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversation, Has.Count.EqualTo(20));
            Assert.That(conversation[^1].Content, Is.EqualTo("Dave: latest"));
        }
    }
}
