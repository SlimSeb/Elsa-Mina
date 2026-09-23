using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Alerts;

[NamedCommand("removealert", "deletealert")]
public class RemoveAlertCommand : Command
{
    private readonly IAlertsManager _alertsManager;

    public RemoveAlertCommand(IAlertsManager alertsManager)
    {
        _alertsManager = alertsManager;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "alerts_remove_help";

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

        var removedAlert =
            await _alertsManager.RemoveAlertAsync(context.RoomId, platform, channelInput, cancellationToken);
        if (removedAlert != null)
        {
            context.ReplyLocalizedMessage("alerts_removed", removedAlert.ChannelName, platform);
        }
        else
        {
            context.ReplyLocalizedMessage("alerts_not_found", channelInput, platform);
        }
    }
}
