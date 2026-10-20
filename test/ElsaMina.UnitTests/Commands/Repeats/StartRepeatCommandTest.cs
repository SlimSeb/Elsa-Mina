using ElsaMina.Commands.Repeats;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Repeats;

public class StartRepeatCommandTest
{
    private IRepeatsManager _repeatsManager;
    private IContext _context;
    private StartRepeatCommand _command;

    [SetUp]
    public void SetUp()
    {
        _repeatsManager = Substitute.For<IRepeatsManager>();
        _context = Substitute.For<IContext>();
        _context.IsPrivateMessage.Returns(true);
        _command = new StartRepeatCommand(_repeatsManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
        Assert.That(_command.RequiredRankInPrivateMessage, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_GetTargetRoomId_ShouldReturnFirstArgument()
    {
        // Arrange
        _context.Target.Returns("French Room, hello, 5");

        // Act & Assert
        Assert.That(_command.GetTargetRoomId(_context), Is.EqualTo("frenchroom"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartRepeatInTargetRoom()
    {
        // Arrange
        _context.Target.Returns("French Room, hello, 5");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _repeatsManager.Received(1).StartRepeatAsync("frenchroom", "hello", TimeSpan.FromMinutes(5),
            Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("repeat_start_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenRoomIsEmpty()
    {
        // Arrange
        _context.Target.Returns(", hello, 5");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("repeat_start_error");
        await _repeatsManager.DidNotReceiveWithAnyArgs().StartRepeatAsync(default, default, default);
    }
}
