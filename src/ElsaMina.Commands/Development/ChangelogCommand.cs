using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Development;

[NamedCommand("changelog")]
public class ChangelogCommand : Command
{
    private const int DEFAULT_COMMIT_COUNT = 5;
    private const int MAX_COMMIT_COUNT = 20;

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Voiced;

    protected virtual string ChangelogFilePath =>
        Path.Combine(AppContext.BaseDirectory, "changelog.txt");

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var count = DEFAULT_COMMIT_COUNT;
        if (!string.IsNullOrWhiteSpace(context.Target) &&
            int.TryParse(context.Target.Trim(), out var parsed))
        {
            count = Math.Clamp(parsed, 1, MAX_COMMIT_COUNT);
        }

        try
        {
            var log = await GetChangelogAsync(count, cancellationToken);
            if (string.IsNullOrWhiteSpace(log))
            {
                context.ReplyLocalizedMessage("changelog_no_commits");
                return;
            }

            context.Reply($"!code {log}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not fetch changelog.");
            context.ReplyLocalizedMessage("changelog_error");
        }
    }

    private async Task<string> GetChangelogAsync(int count, CancellationToken cancellationToken)
    {
        if (!File.Exists(ChangelogFilePath))
        {
            throw new InvalidOperationException("changelog.txt not found in output directory");
        }

        var lines = await File.ReadAllLinesAsync(ChangelogFilePath, cancellationToken);
        return string.Join('\n', lines.Take(count));
    }
}
