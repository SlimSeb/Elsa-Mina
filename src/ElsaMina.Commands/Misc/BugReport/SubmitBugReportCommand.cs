using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.BugReport;

[NamedCommand("submitbugreport")]
public class SubmitBugReportCommand : Command
{
    private static readonly string[] SEPARATOR = ["|||"];

    private readonly IGithubIssueService _githubIssueService;

    public SubmitBugReportCommand(IGithubIssueService githubIssueService)
    {
        _githubIssueService = githubIssueService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsHidden => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (!_githubIssueService.IsConfigured)
        {
            context.ReplyRankAwareLocalizedMessage("bugreport_not_configured");
            return;
        }

        var parts = context.Target.Split(SEPARATOR, 2, StringSplitOptions.None);
        var title = parts[0].Trim();
        var description = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            context.ReplyLocalizedMessage("bugreport_missing_title");
            return;
        }

        var body = $"{description}\n\n---\n*{context.GetString("bugreport_issue_reporter", context.Sender.Name)}*";

        try
        {
            var issue = await _githubIssueService.CreateIssueAsync(title, body, ["bug", "user-reported"],
                cancellationToken);

            if (issue == null)
            {
                context.ReplyLocalizedMessage("bugreport_submit_error");
                return;
            }

            context.ReplyLocalizedMessage("bugreport_submit_success", issue.Number, issue.HtmlUrl);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while creating a Github issue");
            context.ReplyLocalizedMessage("bugreport_submit_error");
        }
    }
}
