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

    private static readonly string CHANGELOG_FILE_PATH =
        Path.Combine(AppContext.BaseDirectory, "changelog.txt");

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Voiced;

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

    private static async Task<string> GetChangelogAsync(int count, CancellationToken cancellationToken)
    {
        if (!File.Exists(CHANGELOG_FILE_PATH))
        {
            throw new InvalidOperationException("changelog.txt not found in output directory");
        }

        var lines = await File.ReadAllLinesAsync(CHANGELOG_FILE_PATH, cancellationToken);
        return string.Join('\n', lines.Take(count));
    }
}
