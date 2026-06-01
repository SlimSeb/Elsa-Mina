namespace ElsaMina.Commands.Misc.BugReport;

public interface IGithubIssueService
{
    bool IsConfigured { get; }

    Task<GithubIssueResponseDto> CreateIssueAsync(string title, string body, IEnumerable<string> labels = null,
        CancellationToken cancellationToken = default);
}
