using System.Globalization;
using ElsaMina.Commands.Misc.Help;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Help;

[TestFixture]
public class CommandInfoCommandTest
{
    private ICommandExecutor _commandExecutor;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private CommandInfoCommand _command;

    [SetUp]
    public void SetUp()
    {
        _commandExecutor = Substitute.For<ICommandExecutor>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();
        _configuration.Trigger.Returns("-");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<CommandInfoViewModel>())
            .Returns("rendered");
        _command = new CommandInfoCommand(_commandExecutor, _templatesManager, _configuration);
    }

    private static ICommand BuildCommand(string name, IEnumerable<string> aliases = null, bool isHidden = false)
    {
        var command = Substitute.For<ICommand>();
        command.Name.Returns(name);
        command.Aliases.Returns(aliases ?? []);
        command.IsHidden.Returns(isHidden);
        return command;
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
    public async Task Test_RunAsync_ShouldReplyUsage_WhenTargetIsEmpty()
    {
        _context.Target.Returns(" ");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("command_info_usage", "-");
        await _templatesManager.DidNotReceive()
            .GetTemplateAsync(Arg.Any<string>(), Arg.Any<CommandInfoViewModel>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenCommandDoesNotExist()
    {
        _context.Target.Returns("doesnotexist");
        var help = BuildCommand("help");
        _commandExecutor.GetAllCommands().Returns([help]);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("command_info_not_found", "doesnotexist");
        await _templatesManager.DidNotReceive()
            .GetTemplateAsync(Arg.Any<string>(), Arg.Any<CommandInfoViewModel>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldFindCommandByName_AndRenderTemplate()
    {
        _context.Target.Returns("help");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        var help = BuildCommand("help");
        _commandExecutor.GetAllCommands().Returns([help]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Help/CommandInfo",
            Arg.Is<CommandInfoViewModel>(vm => vm.Command == help && vm.Trigger == "-"));
        _context.Received(1).ReplyHtml("rendered", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldStripLeadingTrigger_FromTarget()
    {
        _context.Target.Returns("-help");
        var help = BuildCommand("help");
        _commandExecutor.GetAllCommands().Returns([help]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Help/CommandInfo",
            Arg.Is<CommandInfoViewModel>(vm => vm.Command == help));
    }

    [Test]
    public async Task Test_RunAsync_ShouldFindCommandByAlias()
    {
        _context.Target.Returns("about");
        var help = BuildCommand("help", aliases: ["about"]);
        _commandExecutor.GetAllCommands().Returns([help]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Help/CommandInfo",
            Arg.Is<CommandInfoViewModel>(vm => vm.Command == help));
    }

    [Test]
    public async Task Test_RunAsync_ShouldTreatHiddenCommandAsNotFound_WhenSenderIsNotWhitelisted()
    {
        _context.Target.Returns("secret");
        _context.IsSenderWhitelisted.Returns(false);
        var secret = BuildCommand("secret", isHidden: true);
        _commandExecutor.GetAllCommands().Returns([secret]);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("command_info_not_found", "secret");
        await _templatesManager.DidNotReceive()
            .GetTemplateAsync(Arg.Any<string>(), Arg.Any<CommandInfoViewModel>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowHiddenCommand_WhenSenderIsWhitelisted()
    {
        _context.Target.Returns("secret");
        _context.IsSenderWhitelisted.Returns(true);
        var secret = BuildCommand("secret", isHidden: true);
        _commandExecutor.GetAllCommands().Returns([secret]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Help/CommandInfo",
            Arg.Is<CommandInfoViewModel>(vm => vm.Command == secret));
        _context.Received(1).ReplyHtml("rendered", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCultureToViewModel()
    {
        var culture = new CultureInfo("fr-FR");
        _context.Target.Returns("help");
        _context.Culture.Returns(culture);
        var help = BuildCommand("help");
        _commandExecutor.GetAllCommands().Returns([help]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<CommandInfoViewModel>(vm => vm.Culture == culture));
    }
}
