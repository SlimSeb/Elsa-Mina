using ElsaMina.Commands.Showdown.Ladder.EloHistory;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Showdown.Ladder.EloHistory;

public class UntrackEloProgressionCommandTest
{
    private IEloProgressionManager _eloProgressionManager;
    private UntrackEloProgressionCommand _command;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _eloProgressionManager = Substitute.For<IEloProgressionManager>();
        _command = new UntrackEloProgressionCommand(_eloProgressionManager);
        _context = Substitute.For<IContext>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetIsNull()
    {
        // Arrange
        _context.Target.Returns((string)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
        _eloProgressionManager.DidNotReceive().UntrackUser(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenOnlyOnePartProvided()
    {
        // Arrange
        _context.Target.Returns("gen9ou");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
        _eloProgressionManager.DidNotReceive().UntrackUser(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenNormalizedValuesAreEmpty()
    {
        // Arrange
        _context.Target.Returns("!!!, ???");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
        _eloProgressionManager.DidNotReceive().UntrackUser(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUntrackUser_WithNormalizedValues()
    {
        // Arrange
        _context.Target.Returns("Gen 9 OU, Alice Test");
        _eloProgressionManager.UntrackUser("gen9ou", "alicetest").Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _eloProgressionManager.Received(1).UntrackUser("gen9ou", "alicetest");
        _context.Received(1).ReplyLocalizedMessage("untrack_elo_progression_success", "Alice Test", "Gen 9 OU");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenUserWasNotTracked()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        _eloProgressionManager.UntrackUser("gen9ou", "alice").Returns(false);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("untrack_elo_progression_not_found", "alice", "gen9ou");
        _context.DidNotReceive().ReplyLocalizedMessage("untrack_elo_progression_success",
            Arg.Any<object>(), Arg.Any<object>());
    }

    [Test]
    public void Test_RequiredRank_And_HelpMessageKey_ShouldMatchContract()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
            Assert.That(_command.HelpMessageKey, Is.EqualTo("untrack_elo_progression_help"));
        });
    }
}
