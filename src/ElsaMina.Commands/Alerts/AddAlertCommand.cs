using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Alerts;

[NamedCommand("addalert")]
public class AddAlertCommand : Command
{
    private readonly IAlertsManager _alertsManager;
    private readonly IEnumerable<IAlertChannelResolver> _channelResolvers;

    public AddAlertCommand(IAlertsManager alertsManager, IEnumerable<IAlertChannelResolver> channelResolvers)
    {
        _alertsManager = alertsManager;
        _channelResolvers = channelResolvers;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "alerts_add_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (!AlertArguments.TryParse(context.Target, out var platformInput, out var channelInput))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        if (!AlertPlatforms.TryResolve(platformInput, out var platform))
        {
            context.ReplyLocalizedMessage("alerts_unsupported_platform", platformInput,
                string.Join(", ", AlertPlatforms.ALL_PLATFORMS));
            return;
        }

        var resolver = _channelResolvers.FirstOrDefault(channelResolver => channelResolver.CanResolve(platform));
        if (resolver == null || !resolver.IsConfigured)
        {
            context.ReplyLocalizedMessage("alerts_platform_not_configured", platform);
            return;
        }

        AlertChannel channel;
        try
        {
            channel = await resolver.ResolveChannelAsync(channelInput, cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to resolve {Platform} channel {Channel}", platform, channelInput);
            context.ReplyLocalizedMessage("alerts_platform_error", platform);
            return;
        }

        if (channel == null)
        {
            context.ReplyLocalizedMessage("alerts_channel_not_found", channelInput, platform);
            return;
        }

        if (await _alertsManager.AddAlertAsync(context.RoomId, platform, channel, cancellationToken))
        {
            context.ReplyLocalizedMessage("alerts_added", channel.ChannelName, platform);
        }
        else
        {
            context.ReplyLocalizedMessage("alerts_already_exists", channel.ChannelName, platform);
        }
    }
}
