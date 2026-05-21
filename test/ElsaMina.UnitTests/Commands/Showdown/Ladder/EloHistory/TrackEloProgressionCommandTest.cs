using ElsaMina.Commands.Showdown.Ladder.EloHistory;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Showdown.Ladder.EloHistory;

public class TrackEloProgressionCommandTest
{
    private IEloProgressionManager _eloProgressionManager;
    private TrackEloProgressionCommand _command;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _eloProgressionManager = Substitute.For<IEloProgressionManager>();
        _command = new TrackEloProgressionCommand(_eloProgressionManager);
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
        await _eloProgressionManager.DidNotReceive()
            .TrackUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
        await _eloProgressionManager.DidNotReceive()
            .TrackUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
        await _eloProgressionManager.DidNotReceive()
            .TrackUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrackUser_WithNormalizedValues()
    {
        // Arrange
        _context.Target.Returns("Gen 9 OU, Alice Test");
        _eloProgressionManager.TrackUserAsync("gen9ou", "alicetest", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _eloProgressionManager.Received(1)
            .TrackUserAsync("gen9ou", "alicetest", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("track_elo_progression_success", "Alice Test", "Gen 9 OU");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyTracked_WhenUserAlreadyTracked()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        _eloProgressionManager.TrackUserAsync("gen9ou", "alice", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("track_elo_progression_already_tracked", "alice", "gen9ou");
        _context.DidNotReceive().ReplyLocalizedMessage("track_elo_progression_success",
            Arg.Any<object>(), Arg.Any<object>());
    }

    [Test]
    public void Test_RequiredRank_And_HelpMessageKey_ShouldMatchContract()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
            Assert.That(_command.HelpMessageKey, Is.EqualTo("track_elo_progression_help"));
        });
    }
}
