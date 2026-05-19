using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Misc.Help;

[NamedCommand("help", Aliases = ["about"])]
public class HelpCommand : Command
{
    private readonly IVersionProvider _versionProvider;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public HelpCommand(IVersionProvider versionProvider, ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _versionProvider = versionProvider;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "help_command_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var template = await _templatesManager.GetTemplateAsync("Misc/Help/Help", new HelpViewModel
        {
            Culture = context.Culture,
            Version = _versionProvider.Version,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            ReportBugLink = _configuration.BugReportLink,
            RepositoryLink = "https://github.com/SlimSeb/Elsa-Mina"
        });
        context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
    }
}