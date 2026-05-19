using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Development.Commands;

[NamedCommand("allcommands", Aliases = ["all-commands", "commands"])]
public class GetAllCommand : Command
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public GetAllCommand(ICommandExecutor commandExecutor,
        ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _commandExecutor = commandExecutor;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var commands = _commandExecutor.GetAllCommands()
            .Where(command => !command.IsHidden)
            .ToList();

        var categories = commands
            .GroupBy(c => c.Category)
            .OrderBy(g => g.Key)
            .Select(g => string.IsNullOrEmpty(g.Key) ? "Other" : g.Key)
            .ToList();

        var categoryIndex = int.TryParse(context.Target.Trim(), out var parsed)
            ? Math.Clamp(parsed, 0, categories.Count - 1)
            : -1;

        var template = await _templatesManager.GetTemplateAsync("Development/Commands/CommandList",
            new CommandListViewModel
            {
                Commands = commands,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                Categories = categories,
                CurrentCategoryIndex = categoryIndex,
                Culture = context.Culture
            });

        context.ReplyHtmlPage("all-commands",
            template.RemoveNewlines().CollapseAttributeWhitespace());
    }
}
