using System.Globalization;
using ElsaMina.Commands.Misc.Help;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Help;

[TestFixture]
public class HelpCommandTest
{
    private IVersionProvider _versionProvider;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private ICommandExecutor _commandExecutor;
    private IContext _context;
    private HelpCommand _command;

    [SetUp]
    public void SetUp()
    {
        _versionProvider = Substitute.For<IVersionProvider>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _commandExecutor = Substitute.For<ICommandExecutor>();
        _context = Substitute.For<IContext>();

        _versionProvider.Version.Returns("1.0.0");
        _configuration.Name.Returns("Elsa-Mina");
        _configuration.Trigger.Returns("-");
        _configuration.BugReportLink.Returns("https://github.com/SlimSeb/Elsa-Mina/issues");

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<HelpViewModel>())
            .Returns("help rendered");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<CommandInfoViewModel>())
            .Returns("command info rendered");

        _command = new HelpCommand(_versionProvider, _templatesManager, _configuration, _commandExecutor);
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
    public void Test_HelpMessageKey_ShouldBeHelpCommandHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("help_command_help"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task Test_RunAsync_ShouldRenderHelpTemplate_WhenTargetIsNullOrWhitespace(string target)
    {
        _context.Target.Returns(target);
        var culture = new CultureInfo("en-US");
        _context.Culture.Returns(culture);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Help/Help",
            Arg.Is<HelpViewModel>(vm =>
                vm.Culture == culture &&
                vm.Version == "1.0.0" &&
                vm.BotName == "Elsa-Mina" &&
                vm.Trigger == "-" &&
                vm.ReportBugLink == "https://github.com/SlimSeb/Elsa-Mina/issues" &&
                vm.RepositoryLink == "https://github.com/SlimSeb/Elsa-Mina"));
        _context.Received(1).ReplyHtml("help rendered", rankAware: true);
        _commandExecutor.DidNotReceive().GetAllCommands();
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
            .GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldFindCommandByName_AndRenderCommandInfoTemplate()
    {
        _context.Target.Returns("ping");
        var culture = new CultureInfo("en-US");
        _context.Culture.Returns(culture);
        var ping = BuildCommand("ping");
        _commandExecutor.GetAllCommands().Returns([ping]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Help/CommandInfo",
            Arg.Is<CommandInfoViewModel>(vm => vm.Command == ping && vm.Trigger == "-" && vm.Culture == culture));
        _context.Received(1).ReplyHtml("command info rendered", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldStripLeadingTrigger_FromTarget()
    {
        _context.Target.Returns("-ping");
        var ping = BuildCommand("ping");
        _commandExecutor.GetAllCommands().Returns([ping]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Help/CommandInfo",
            Arg.Is<CommandInfoViewModel>(vm => vm.Command == ping));
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
        _context.Received(1).ReplyHtml("command info rendered", rankAware: true);
    }
}
