using ElsaMina.Commands.Tournaments.Trade;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Tournaments.Trade;

public class NoTradeCommandTest
{
    private IContext _context;
    private NoTradeCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");
        _context.GetString(Arg.Any<string>()).Returns(string.Empty);
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>()).Returns(string.Empty);
        _command = new NoTradeCommand();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        // Arrange
        // (no additional setup)

        // Act
        var rank = _command.RequiredRank;

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        // Arrange
        // (no additional setup)

        // Act
        var allowed = _command.IsAllowedInPrivateMessage;

        // Assert
        Assert.That(allowed, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>());
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetHasFewerThanTwoParts()
    {
        // Arrange
        _context.Target.Returns("someuser");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>());
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendRefusalToStaffRoom_WhenTargetIsValid()
    {
        // Arrange
        _context.Target.Returns("someuser, 3");
        _context.GetString("notrade_staff_refused", "someuser").Returns("Trade refusé pour someuser");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).SendMessageIn(
            "frenchstaff",
            Arg.Is<string>(msg => msg.Contains("trade-req-someuser-3")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeStaffRefusalMessage_WhenTargetIsValid()
    {
        // Arrange
        _context.Target.Returns("someuser, 3");
        _context.GetString("notrade_staff_refused", "someuser").Returns("Trade refusé pour someuser");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).SendMessageIn(
            "frenchstaff",
            Arg.Is<string>(msg => msg.Contains("Trade refusé pour someuser")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimUsernameAndPoints_WhenTargetHasWhitespace()
    {
        // Arrange
        _context.Target.Returns("  alice  ,  5  ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).SendMessageIn(
            "frenchstaff",
            Arg.Is<string>(msg => msg.Contains("trade-req-alice-5")));
    }
}
