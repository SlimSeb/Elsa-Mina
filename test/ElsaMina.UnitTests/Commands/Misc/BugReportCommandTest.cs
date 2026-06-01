using ElsaMina.Commands.Misc.BugReport;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc;

public class BugReportCommandTest
{
    private IConfiguration _configuration;
    private IGithubIssueService _githubIssueService;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private BugReportCommand _command;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _githubIssueService = Substitute.For<IGithubIssueService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _context = Substitute.For<IContext>();
        _command = new BugReportCommand(_configuration, _githubIssueService, _templatesManager);
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
    public async Task Test_RunAsync_ShouldDisplayPanel_WhenGithubIsConfigured()
    {
        _githubIssueService.IsConfigured.Returns(true);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult("<html></html>"));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("bug-report", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithLink_WhenBugReportLinkIsConfigured()
    {
        _githubIssueService.IsConfigured.Returns(false);
        _configuration.BugReportLink.Returns("https://github.com/example/issues");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("bugreport_reply", "https://github.com/example/issues");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotConfigured_WhenNothingIsConfigured()
    {
        _githubIssueService.IsConfigured.Returns(false);
        _configuration.BugReportLink.Returns((string)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("bugreport_not_configured");
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage("bugreport_reply", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotConfigured_WhenBugReportLinkIsWhitespace()
    {
        _githubIssueService.IsConfigured.Returns(false);
        _configuration.BugReportLink.Returns("   ");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("bugreport_not_configured");
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage("bugreport_reply", Arg.Any<object[]>());
    }
}
