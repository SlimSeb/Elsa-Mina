using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Misc.BugReport;

[NamedCommand("bugreport", "bug", "issue")]
public class BugReportCommand : Command
{
    private readonly IConfiguration _configuration;
    private readonly IGithubIssueService _githubIssueService;
    private readonly ITemplatesManager _templatesManager;

    public BugReportCommand(IConfiguration configuration,
        IGithubIssueService githubIssueService,
        ITemplatesManager templatesManager)
    {
        _configuration = configuration;
        _githubIssueService = githubIssueService;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "bugreport_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (_githubIssueService.IsConfigured)
        {
            var template = await _templatesManager.GetTemplateAsync("Misc/BugReport/BugReportPanel",
                new BugReportPanelViewModel
                {
                    Culture = context.Culture,
                    BotName = _configuration.Name,
                    Trigger = _configuration.Trigger
                });

            context.ReplyHtmlPage("bug-report", template.RemoveNewlines().CollapseAttributeWhitespace());
            return;
        }

        if (!string.IsNullOrWhiteSpace(_configuration.BugReportLink))
        {
            context.ReplyRankAwareLocalizedMessage("bugreport_reply", _configuration.BugReportLink);
            return;
        }

        context.ReplyRankAwareLocalizedMessage("bugreport_not_configured");
    }
}
