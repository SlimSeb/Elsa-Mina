using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Users.Streaks;

[NamedCommand("streak")]
public class StreakCommand : Command
{
    private readonly IStreakService _streakService;

    public StreakCommand(IStreakService streakService)
    {
        _streakService = streakService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => false;
    public override string HelpMessageKey => "streak_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var targetUserId = string.IsNullOrWhiteSpace(context.Target)
            ? context.Sender.UserId
            : context.Target.Trim().ToLowerAlphaNum();

        try
        {
            var (current, longest) = await _streakService.GetStreakAsync(targetUserId, context.RoomId, cancellationToken);

            if (current == 0 && longest == 0)
            {
                context.ReplyLocalizedMessage("streak_no_data", targetUserId);
                return;
            }

            context.ReplyLocalizedMessage("streak_result", targetUserId, current, longest);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get streak for user {0}", targetUserId);
            context.ReplyLocalizedMessage("streak_error");
        }
    }
}
