using System.Globalization;
using ElsaMina.Commands.Development.Commands;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Development.Commands;

[TestFixture]
public class GetAllCommandTest
{
    private ICommandExecutor _commandExecutor;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private GetAllCommand _command;

    [SetUp]
    public void SetUp()
    {
        _commandExecutor = Substitute.For<ICommandExecutor>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();
        _command = new GetAllCommand(_commandExecutor, _templatesManager, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassOnlyVisibleCommandsToTemplate()
    {
        var visibleCommand = Substitute.For<ICommand>();
        visibleCommand.IsHidden.Returns(false);
        var hiddenCommand = Substitute.For<ICommand>();
        hiddenCommand.IsHidden.Returns(true);

        _commandExecutor.GetAllCommands().Returns([visibleCommand, hiddenCommand]);
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<CommandListViewModel>())
            .Returns("rendered");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Development/Commands/CommandList",
            Arg.Is<CommandListViewModel>(vm =>
                vm.Commands.Count() == 1 &&
                vm.Commands.Single() == visibleCommand));
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCultureToViewModel()
    {
        var culture = new CultureInfo("fr-FR");
        _commandExecutor.GetAllCommands().Returns([]);
        _context.Culture.Returns(culture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<CommandListViewModel>())
            .Returns("rendered");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<CommandListViewModel>(vm => vm.Culture == culture));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtmlPage_WithProcessedTemplate()
    {
        _commandExecutor.GetAllCommands().Returns([]);
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<CommandListViewModel>())
            .Returns("<div>\ntest\n</div>");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("all-commands", Arg.Is<string>(s => !s.Contains('\n')));
    }
}
