using ElsaMina.Battles.Commands;
using ElsaMina.Battles.Replays;
using ElsaMina.Core.Contexts;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Commands;

public class SaveReplaysCommandTest
{
    private const string DIRECTORY = "/bot/replays";

    private IReplaySavingService _replaySavingService;
    private IContext _context;
    private SaveReplaysCommand _command;

    [SetUp]
    public void SetUp()
    {
        _replaySavingService = Substitute.For<IReplaySavingService>();
        _replaySavingService.Status.Returns(new ReplaySavingStatus(true, DIRECTORY, 12, 1, 0, "gen9randombattle-1"));
        _context = Substitute.For<IContext>();
        _command = new SaveReplaysCommand(_replaySavingService);
    }

    [TestCase("")]
    [TestCase(" on ")]
    public async Task Test_RunAsync_ShouldStartSaving_WhenNoArgumentOrOn(string target)
    {
        // Arrange
        _context.Target.Returns(target);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _replaySavingService.Received(1).Start();
        _context.Received(1).Reply(Arg.Is<string>(message => message.Contains(DIRECTORY)));
    }

    [TestCase("off")]
    [TestCase("STOP")]
    public async Task Test_RunAsync_ShouldStopSaving_WhenOff(string target)
    {
        // Arrange
        _context.Target.Returns(target);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _replaySavingService.Received(1).Stop();
        _context.Received(1).Reply(Arg.Is<string>(message => message.Contains("12 replays saved")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowUsage_WhenArgumentIsUnknown()
    {
        // Arrange
        _context.Target.Returns("maybe");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _replaySavingService.DidNotReceive().Start();
        _replaySavingService.DidNotReceive().Stop();
        _context.Received(1).Reply(Arg.Is<string>(message => message.StartsWith("Usage")));
    }
}
