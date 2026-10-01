using ElsaMina.Commands.JoinPhrases;
using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.JoinPhrases;

public class SetJoinPhraseCommandTest
{
    private IRoomUserDataService _roomUserDataService;
    private SetJoinPhraseCommand _command;

    private const string TEST_ROOM_ID = "testroom";
    private const string TEST_USER_ID = "testuser";
    private const string JOIN_PHRASE = "Welcome back!";

    [SetUp]
    public void SetUp()
    {
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _command = new SetJoinPhraseCommand(_roomUserDataService);
    }

    [Test]
    public void Test_RequiredRank_ShouldReturnCorrectRank()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldReturnCorrectKey()
    {
        // Assert
        Assert.That(_command.HelpMessageKey, Is.EqualTo("setjoinphrase_help_message"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenArgumentsAreInvalid()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Command.Returns("setjoinphrase");
        context.Target.Returns("invalidInput");

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).GetString(_command.HelpMessageKey);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetJoinPhrase_WhenArgumentsAreValid()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Command.Returns("setjoinphrase");
        context.Target.Returns($"{TEST_USER_ID},{JOIN_PHRASE}");
        context.RoomId.Returns(TEST_ROOM_ID);

        // Act
        await _command.RunAsync(context);

        // Assert
        await _roomUserDataService.Received(1).SetUserJoinPhraseAsync(TEST_ROOM_ID, TEST_USER_ID, JOIN_PHRASE);
        context.Received(1).ReplyLocalizedMessage("setjoinphrase_success");
    }

    [TestCase("deletejoinphrase")]
    [TestCase("delete-joinphrase")]
    [TestCase("removejoinphrase")]
    [TestCase("remove-joinphrase")]
    public async Task Test_RunAsync_ShouldRemoveJoinPhrase_WhenCommandIsRemove(string command)
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Command.Returns(command);
        context.Target.Returns(TEST_USER_ID);
        context.RoomId.Returns(TEST_ROOM_ID);

        // Act
        await _command.RunAsync(context);

        // Assert
        await _roomUserDataService.Received(1).SetUserJoinPhraseAsync(TEST_ROOM_ID, TEST_USER_ID, string.Empty);
        context.Received(1).ReplyLocalizedMessage("setjoinphrase_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailureMessage_WhenExceptionOccurs()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Command.Returns("setjoinphrase");
        context.Target.Returns($"{TEST_USER_ID},{JOIN_PHRASE}");
        context.RoomId.Returns(TEST_ROOM_ID);

        var exception = new Exception("Database error");
        _roomUserDataService.SetUserJoinPhraseAsync(TEST_ROOM_ID, TEST_USER_ID, JOIN_PHRASE).Throws(exception);

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyLocalizedMessage("setjoinphrase_failure", exception.Message);
    }
}