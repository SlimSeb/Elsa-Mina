using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Users.Streaks;

public class StreakUpdateHandler : ChatMessageHandler
{
    private readonly IStreakService _streakService;
    private readonly IClockService _clockService;
    private readonly IConfiguration _configuration;

    public StreakUpdateHandler(IContextFactory contextFactory,
        IStreakService streakService,
        IClockService clockService,
        IConfiguration configuration) : base(contextFactory)
    {
        _streakService = streakService;
        _clockService = clockService;
        _configuration = configuration;
    }

    public override async Task HandleMessageAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Sender.UserId == _configuration.Name.ToLowerAlphaNum())
        {
            return;
        }

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(_clockService.CurrentUtcDateTime, context.Room.TimeZone));

        await _streakService.UpdateStreakAsync(context.Sender.UserId, context.RoomId, today, cancellationToken);
    }
}
