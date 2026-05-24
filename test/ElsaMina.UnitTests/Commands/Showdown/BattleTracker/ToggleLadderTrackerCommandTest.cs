using ElsaMina.Commands.Showdown.BattleTracker;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Showdown.BattleTracker;

public class ToggleLadderTrackerCommandTest
{
    private ILadderTrackerManager _ladderTrackerManager;
    private ToggleLadderTrackerCommand _command;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _ladderTrackerManager = Substitute.For<ILadderTrackerManager>();
        _command = new ToggleLadderTrackerCommand(_ladderTrackerManager);
        _context = Substitute.For<IContext>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUsage_WhenTargetIsInvalid()
    {
        // Arrange
        _context.Target.Returns("gen9ou");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
        _ladderTrackerManager.DidNotReceive()
            .StartTracking(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUsage_WhenRoomIdIsMissing()
    {
        // Arrange
        _context.Target.Returns("gen9ou,prefix");
        _context.RoomId.Returns((string)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
        _ladderTrackerManager.DidNotReceive()
            .StartTracking(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartTracking_WithNormalizedValues()
    {
        // Arrange
        _context.RoomId.Returns("ou-room");
        _context.Target.Returns("Gen 9 OU, AB-c");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _ladderTrackerManager.Received(1).StartTracking("ou-room", "gen9ou", "abc");
        _context.Received(1).ReplyLocalizedMessage("toggletracking_success", "gen9ou", "abc");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUsage_WhenNormalizedFormatOrPrefixIsEmpty()
    {
        // Arrange
        _context.RoomId.Returns("ou-room");
        _context.Target.Returns("!!!,???");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
        _ladderTrackerManager.DidNotReceive()
            .StartTracking(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailureMessage_WhenTrackingThrows()
    {
        // Arrange
        _context.RoomId.Returns("ou-room");
        _context.Target.Returns("gen9ou,prefix");
        _ladderTrackerManager
            .When(manager => manager.StartTracking("ou-room", "gen9ou", "prefix"))
            .Do(_ => throw new Exception("boom"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("toggletracking_failure");
    }

    [Test]
    public void Test_HelpMessageKey_AndRequiredRank_ShouldMatchCommandContract()
    {
        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.HelpMessageKey, Is.EqualTo("toggletracking_help_message"));
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
        }
    }
}
