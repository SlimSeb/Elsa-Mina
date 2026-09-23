using Autofac;
using ElsaMina.Commands.Alerts;
using ElsaMina.Commands.Alerts.Twitch;
using ElsaMina.Commands.Alerts.Twitter;
using ElsaMina.Commands.Alerts.Youtube;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class AlertsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<AddAlertCommand>();
        builder.RegisterCommand<RemoveAlertCommand>();
        builder.RegisterCommand<AlertsListCommand>();

        builder.RegisterType<TwitchApiClient>().As<ITwitchApiClient>().As<IAlertChannelResolver>().SingleInstance();
        builder.RegisterType<YoutubeAlertsApiClient>().As<IYoutubeAlertsApiClient>().As<IAlertChannelResolver>()
            .SingleInstance();
        builder.RegisterType<TwitterApiClient>().As<ITwitterApiClient>().As<IAlertChannelResolver>().SingleInstance();

        builder.RegisterType<AlertsManager>().As<IAlertsManager>().SingleInstance().OnActivating(e =>
        {
            e.Instance.InitializeAsync().Wait();
        }).AutoActivate();

        builder.RegisterType<TwitchLiveAlertsService>().As<ITwitchLiveAlertsService>().SingleInstance()
            .OnActivating(e => e.Instance.Start()).AutoActivate();
        builder.RegisterType<YoutubeAlertsService>().As<IYoutubeAlertsService>().SingleInstance()
            .OnActivating(e => e.Instance.Start()).AutoActivate();
        builder.RegisterType<TwitterAlertsService>().As<ITwitterAlertsService>().SingleInstance()
            .OnActivating(e => e.Instance.Start()).AutoActivate();
    }
}
