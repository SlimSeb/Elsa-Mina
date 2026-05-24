using ElsaMina.Commands.Misc;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc;

public class BugReportCommandTest
{
    private IConfiguration _configuration;
    private IContext _context;
    private BugReportCommand _command;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();
        _command = new BugReportCommand(_configuration);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.Name, Is.EqualTo("bugreport"));
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
            Assert.That(_command.HelpMessageKey, Is.EqualTo("bugreport_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithLink_WhenBugReportLinkIsConfigured()
    {
        _configuration.BugReportLink.Returns("https://github.com/example/issues");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("bugreport_reply", "https://github.com/example/issues");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotConfigured_WhenBugReportLinkIsNull()
    {
        _configuration.BugReportLink.Returns((string)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("bugreport_not_configured");
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage("bugreport_reply", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotConfigured_WhenBugReportLinkIsEmpty()
    {
        _configuration.BugReportLink.Returns(string.Empty);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("bugreport_not_configured");
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage("bugreport_reply", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotConfigured_WhenBugReportLinkIsWhitespace()
    {
        _configuration.BugReportLink.Returns("   ");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("bugreport_not_configured");
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage("bugreport_reply", Arg.Any<object[]>());
    }
}
