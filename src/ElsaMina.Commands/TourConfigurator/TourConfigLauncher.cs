using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.TourConfigurator;

public class TourConfigLauncher : IDynamicCommandProvider
{
    private readonly ITourConfigService _tourConfigService;

    public TourConfigLauncher(ITourConfigService tourConfigService)
    {
        _tourConfigService = tourConfigService;
    }

    public async Task<bool> TryExecuteAsync(string commandName, IContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.IsPrivateMessage)
        {
            return false;
        }

        if (!context.HasRankOrHigher(Rank.Driver))
        {
            return false;
        }

        var config = await _tourConfigService.GetTourConfigAsync(commandName, context.RoomId, cancellationToken);
        if (config == null)
        {
            return false;
        }

        LaunchTournament(context, config);
        return true;
    }

    internal static void LaunchTournament(IContext context, DataAccess.Models.TourConfig config)
    {
        var roomId = context.IsPrivateMessage ? null : context.RoomId;

        context.SendMessageIn(roomId ?? config.RoomId, $"/tour create {config.Tier}, {config.Format}");

        if (config.Autostart > 0)
        {
            context.SendMessageIn(roomId ?? config.RoomId, $"/tour autostart {config.Autostart}");
        }

        if (config.AutoDq.HasValue)
        {
            context.SendMessageIn(roomId ?? config.RoomId, $"/tour autodq {config.AutoDq.Value}");
        }

        if (!string.IsNullOrWhiteSpace(config.TourName))
        {
            context.SendMessageIn(roomId ?? config.RoomId, $"/tour name {config.TourName}");
        }

        if (!string.IsNullOrWhiteSpace(config.Rules))
        {
            context.SendMessageIn(roomId ?? config.RoomId, $"/tour rules {config.Rules}");
        }
    }
}
