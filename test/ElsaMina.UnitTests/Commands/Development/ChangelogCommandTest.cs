using ElsaMina.Commands.Development;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Development;

[TestFixture]
public class ChangelogCommandTest
{
    private IContext _context;
    private string _tempFile;
    private TestableChangelogCommand _command;

    private class TestableChangelogCommand(string filePath) : ChangelogCommand
    {
        protected override string ChangelogFilePath => filePath;
    }

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _tempFile = Path.GetTempFileName();
        _command = new TestableChangelogCommand(_tempFile);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithDefaultFiveCommits_WhenNoTargetGiven()
    {
        var lines = Enumerable.Range(1, 10).Select(i => $"commit{i}").ToArray();
        await File.WriteAllLinesAsync(_tempFile, lines);
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        var expected = string.Join('\n', lines.Take(5));
        _context.Received(1).Reply($"!code {expected}");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithRequestedCount_WhenTargetIsValidNumber()
    {
        var lines = Enumerable.Range(1, 10).Select(i => $"commit{i}").ToArray();
        await File.WriteAllLinesAsync(_tempFile, lines);
        _context.Target.Returns("3");

        await _command.RunAsync(_context);

        var expected = string.Join('\n', lines.Take(3));
        _context.Received(1).Reply($"!code {expected}");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClampCountToMax_WhenTargetExceedsMaximum()
    {
        var lines = Enumerable.Range(1, 25).Select(i => $"commit{i}").ToArray();
        await File.WriteAllLinesAsync(_tempFile, lines);
        _context.Target.Returns("99");

        await _command.RunAsync(_context);

        var expected = string.Join('\n', lines.Take(20));
        _context.Received(1).Reply($"!code {expected}");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClampCountToMin_WhenTargetIsZeroOrNegative()
    {
        var lines = Enumerable.Range(1, 10).Select(i => $"commit{i}").ToArray();
        await File.WriteAllLinesAsync(_tempFile, lines);
        _context.Target.Returns("0");

        await _command.RunAsync(_context);

        var expected = lines[0];
        _context.Received(1).Reply($"!code {expected}");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoCommits_WhenFileIsEmpty()
    {
        await File.WriteAllTextAsync(_tempFile, string.Empty);
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("changelog_no_commits");
        _context.DidNotReceive().Reply(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenFileDoesNotExist()
    {
        File.Delete(_tempFile);
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("changelog_error");
        _context.DidNotReceive().Reply(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseDefaultCount_WhenTargetIsNotANumber()
    {
        var lines = Enumerable.Range(1, 10).Select(i => $"commit{i}").ToArray();
        await File.WriteAllLinesAsync(_tempFile, lines);
        _context.Target.Returns("notanumber");

        await _command.RunAsync(_context);

        var expected = string.Join('\n', lines.Take(5));
        _context.Received(1).Reply($"!code {expected}");
    }
}
