using System.Diagnostics;
using System.Text;
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
            var log = await GetGitLogAsync(count, cancellationToken);
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

    private static async Task<string> GetGitLogAsync(int count, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git",
            $"log --pretty=format:\"%h %ad %an: %s\" --date=short -n {count}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git log exited with code {process.ExitCode}");
        }

        return output.TrimEnd();
    }
}
