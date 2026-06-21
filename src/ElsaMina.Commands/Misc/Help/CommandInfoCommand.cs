using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Misc.Help;

[NamedCommand("command", Aliases = ["commandinfo", "cmdinfo"])]
public class CommandInfoCommand : Command
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public CommandInfoCommand(ICommandExecutor commandExecutor,
        ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _commandExecutor = commandExecutor;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "command_info_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var target = context.Target?.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            context.ReplyLocalizedMessage("command_info_usage", _configuration.Trigger);
            return;
        }

        var query = target.TrimStart((_configuration.Trigger ?? string.Empty).ToCharArray());
        var command = _commandExecutor.GetAllCommands()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, query, StringComparison.OrdinalIgnoreCase)
                || candidate.Aliases.Any(alias => string.Equals(alias, query, StringComparison.OrdinalIgnoreCase)));

        if (command == null || (command.IsHidden && !context.IsSenderWhitelisted))
        {
            context.ReplyLocalizedMessage("command_info_not_found", query);
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Misc/Help/CommandInfo", new CommandInfoViewModel
        {
            Command = command,
            Trigger = _configuration.Trigger,
            Culture = context.Culture
        });
        context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
    }
}
