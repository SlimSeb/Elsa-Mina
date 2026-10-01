using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Profile;

public class SetAvatarCommandTest
{
    private SetAvatarCommand _command;
    private IRoomUserDataService _roomUserDataService;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _command = new SetAvatarCommand(_roomUserDataService);
        _context = Substitute.For<IContext>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetAvatar_WhenValidParametersProvided()
    {
        // Arrange
        _context.RoomId.Returns("testRoom");
        _context.Target.Returns("user123, https://avatar.url/image.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _roomUserDataService.Received(1).SetUserAvatarAsync("testRoom", "user123", "https://avatar.url/image.png");
        _context.Received(1).ReplyLocalizedMessage("avatar_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReturnHelpMessage_WhenIncorrectParameterCount()
    {
        // Arrange
        _context.Target.Returns("user123");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage(_command.HelpMessageKey);
        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserAvatarAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleExceptionAndSendFailureMessage_WhenSetAvatarFails()
    {
        // Arrange
        _context.RoomId.Returns("testRoom");
        _context.Target.Returns("user123, https://avatar.url/image.png");
        const string errorMessage = "Database error";
        _roomUserDataService.SetUserAvatarAsync("testRoom", "user123", "https://avatar.url/image.png")
            .Throws(new Exception(errorMessage));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("avatar_failure", errorMessage);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeCorrect()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeCorrect()
    {
        // Assert
        Assert.That(_command.HelpMessageKey, Is.EqualTo("avatar_help_message"));
    }
}