using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc;

[NamedCommand("ship")]
public class ShipCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "ship_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            ReplyLocalizedHelpMessage(context);
            return Task.CompletedTask;
        }

        var nameA = parts[0];
        var nameB = parts[1];
        var score = ComputeScore(nameA, nameB);
        var emoji = GetEmoji(score);

        context.ReplyRankAwareLocalizedMessage("ship_result", nameA, nameB, score, emoji);
        return Task.CompletedTask;
    }

    private static int ComputeScore(string nameA, string nameB)
    {
        var combined = string.Concat(
            nameA.ToLowerInvariant().Where(char.IsLetterOrDigit),
            nameB.ToLowerInvariant().Where(char.IsLetterOrDigit));

        var hash = combined.Aggregate(5381, (current, character) => current * 33 ^ character);
        return Math.Abs(hash) % 101;
    }

    private static string GetEmoji(int score) => score switch
    {
        >= 90 => "💞",
        >= 70 => "❤️",
        >= 50 => "💛",
        >= 30 => "🤝",
        _ => "💔"
    };
}
