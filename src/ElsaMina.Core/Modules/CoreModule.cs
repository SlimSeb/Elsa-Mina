using System.Resources;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Handlers.DefaultHandlers.Rooms;
using ElsaMina.Core.Services.BattleTracker;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Core.Services.FeatureSwitches;
using ElsaMina.Core.Services.Formats;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.Login;
using ElsaMina.Core.Services.PrivateMessages;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.RoomInfo;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Scheduling;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Services.UserData;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Core.Utils;
using Assembly = System.Reflection.Assembly;

namespace ElsaMina.Core.Modules;

public class CoreModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterInstance(
                new ResourceManager("ElsaMina.Core.Resources.Resources", Assembly.GetExecutingAssembly()))
            .As<ResourceManager>().SingleInstance();

        builder.RegisterType<FeatureSwitchService>().As<IFeatureSwitchService>().SingleInstance();
        builder.RegisterType<TelemetryService>().As<ITelemetryService>().SingleInstance();
        builder.RegisterType<HttpService>().As<IHttpService>().SingleInstance();
        builder.RegisterType<ClockService>().As<IClockService>().SingleInstance();
        builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
        builder.RegisterType<CommandRegistry>().As<ICommandRegistry>().SingleInstance();
        builder.RegisterType<CommandExecutor>().As<ICommandExecutor>().As<IBotLifecycleParticipant>().SingleInstance();
        builder.RegisterType<RoomsManager>().As<IRoomsManager>().SingleInstance();
        builder.RegisterType<RoomFactory>().As<IRoomFactory>().SingleInstance();
        builder.RegisterType<FormatsManager>().As<IFormatsManager>().SingleInstance();
        builder.RegisterType<LoginService>().As<ILoginService>().SingleInstance();
        builder.RegisterType<ResourcesService>().As<IResourcesService>().SingleInstance();
        builder.RegisterType<PmSendersManager>().As<IPmSendersManager>().SingleInstance();
        builder.RegisterType<HandlerManager>().As<IHandlerManager>().SingleInstance();
        builder.Register(componentContext => new AutofacServiceProvider(componentContext.Resolve<ILifetimeScope>()))
            .As<IServiceProvider>().SingleInstance();
        builder.RegisterType<TemplatesManager>().As<ITemplatesManager>().As<IBotLifecycleParticipant>().SingleInstance();
        builder.RegisterType<UserDetailsManager>().As<IUserDetailsManager>().SingleInstance();
        builder.RegisterType<ActiveBattlesManager>().As<IActiveBattlesManager>().SingleInstance();
        builder.RegisterType<RoomInfoManager>().As<IRoomInfoManager>().SingleInstance();
        builder.RegisterType<UserDataService>().As<IUserDataService>().SingleInstance();
        builder.RegisterType<RandomService>().As<IRandomService>().SingleInstance();
        builder.RegisterType<SystemService>().As<ISystemService>().SingleInstance();
        builder.RegisterType<CoreRoomParameters>().As<IRoomParameterProvider>().SingleInstance();
        builder.RegisterType<ParametersDefinitionFactory>().As<IParametersDefinitionFactory>()
            .SingleInstance();
        builder.RegisterType<BotLifecycleService>().As<IBotLifecycleService>().SingleInstance();
        builder.RegisterType<ImageService>().As<IImageService>().SingleInstance();
        builder.RegisterType<RoomParameterStore>().As<IRoomParameterStore>();

        builder.RegisterType<KeyedTaskQueue>().As<IKeyedTaskQueue>();
        builder.RegisterType<IncomingMessageDispatcher>().As<IIncomingMessageDispatcher>().SingleInstance();
        builder.RegisterType<OutgoingMessageQueue>().As<IOutgoingMessageQueue>().SingleInstance();
        builder.RegisterType<Client>().As<IClient>().SingleInstance();
        builder.RegisterType<Bot>().As<IBot>().AsSelf().SingleInstance();

        builder.RegisterHandler<ChatMessageCommandHandler>();
        builder.RegisterHandler<PrivateMessageCommandHandler>();
        builder.RegisterHandler<NameTakenHandler>();
        builder.RegisterHandler<QueryResponseHandler>();
        builder.RegisterHandler<RoomsHandler>();
        builder.RegisterHandler<CheckConnectionHandler>();
        builder.RegisterHandler<FormatsHandler>();
        builder.RegisterHandler<LoginHandler>();
        builder.RegisterHandler<AcceptChallengeHandler>();
        builder.RegisterHandler<ErrorHandler>();
    }
}