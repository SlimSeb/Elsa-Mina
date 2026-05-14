using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Users.Streaks;

[NamedCommand("streakboard", Aliases = ["streakleaderboard", "topstreaks"])]
public class StreakLeaderboardCommand : Command
{
    private const int TOP_COUNT = 20;

    private readonly IBotDbContextFactory _botDbContextFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IRoomsManager _roomsManager;

    public StreakLeaderboardCommand(IBotDbContextFactory botDbContextFactory,
        ITemplatesManager templatesManager,
        IRoomsManager roomsManager)
    {
        _botDbContextFactory = botDbContextFactory;
        _templatesManager = templatesManager;
        _roomsManager = roomsManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "streak_leaderboard_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var roomId = string.IsNullOrWhiteSpace(context.Target)
                ? context.RoomId
                : context.Target.ToLowerAlphaNum();

            await using var dbContext = await _botDbContextFactory.CreateDbContextAsync(cancellationToken);
            var topUsers = await dbContext.RoomUsers
                .Where(ru => ru.RoomId == roomId && ru.CurrentStreak > 0)
                .Include(ru => ru.User)
                .OrderByDescending(ru => ru.CurrentStreak)
                .ThenByDescending(ru => ru.LongestStreak)
                .Take(TOP_COUNT)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (topUsers.Count == 0)
            {
                context.ReplyLocalizedMessage("streak_leaderboard_no_data");
                return;
            }

            var topList = topUsers
                .Select((ru, i) => new StreakLeaderboardEntry(
                    Rank: i + 1,
                    UserId: ru.Id,
                    UserName: ru.User?.UserName ?? ru.Id,
                    CurrentStreak: ru.CurrentStreak,
                    LongestStreak: ru.LongestStreak))
                .ToList();

            var roomLabel = _roomsManager.GetRoom(roomId)?.Name ?? roomId;
            var template = await _templatesManager.GetTemplateAsync("Users/Streaks/StreakLeaderboard",
                new StreakLeaderboardViewModel
                {
                    Culture = context.Culture,
                    Room = roomLabel,
                    TopList = topList
                });

            context.ReplyHtml(template.RemoveNewlines().RemoveWhitespacesBetweenTags(), rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get streak leaderboard");
            context.ReplyLocalizedMessage("streak_leaderboard_error");
        }
    }
}
