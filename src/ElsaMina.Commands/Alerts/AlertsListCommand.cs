using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Alerts;

[NamedCommand("alerts", "alertlist", "listalerts")]
public class AlertsListCommand : Command
{
    private readonly IAlertsManager _alertsManager;

    public AlertsListCommand(IAlertsManager alertsManager)
    {
        _alertsManager = alertsManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "alerts_list_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var alerts = _alertsManager.GetRoomAlerts(context.RoomId);
        if (alerts.Count == 0)
        {
            context.ReplyLocalizedMessage("alerts_list_empty");
            return Task.CompletedTask;
        }

        var formattedAlerts = string.Join(", ", alerts.Select(alert => $"{alert.ChannelName} ({alert.Platform})"));
        context.ReplyLocalizedMessage("alerts_list", formattedAlerts);
        return Task.CompletedTask;
    }
}
