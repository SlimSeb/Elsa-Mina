using Autofac;
using ElsaMina.Commands.ChatLog;
using ElsaMina.Commands.CustomCommands;
using ElsaMina.Commands.Development;
using ElsaMina.Commands.Economy;
using ElsaMina.Commands.EventAnnounces;
using ElsaMina.Commands.JoinPhrases;
using ElsaMina.Commands.Polls;
using ElsaMina.Commands.Polls.Suggestions;
using ElsaMina.Commands.Repeats;
using ElsaMina.Commands.Repeats.Form;
using ElsaMina.Commands.Repeats.List;
using ElsaMina.Commands.Shop;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class RoomsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<EconomyRoomParameters>().As<IRoomParameterProvider>().SingleInstance();
        builder.RegisterType<EventAnnouncesRoomParameters>().As<IRoomParameterProvider>().SingleInstance();

        builder.RegisterCommand<AddCustomCommand>();
        builder.RegisterCommand<CustomCommandList>();
        builder.RegisterCommand<DeleteCustomCommand>();
        builder.RegisterCommand<EditCustomCommand>();
        builder.RegisterCommand<RandomCustomCommand>();
        builder.RegisterCommand<SetJoinPhraseCommand>();
        builder.RegisterCommand<GiveMoneyCommand>();
        builder.RegisterCommand<TransferMoneyCommand>();
        builder.RegisterCommand<MoneyCommand>();
        builder.RegisterCommand<MoneyLeaderboardCommand>();
        builder.RegisterCommand<RepeatFormCommand>();
        builder.RegisterCommand<StartRepeatCommand>();
        builder.RegisterCommand<StopRepeatCommand>();
        builder.RegisterCommand<RepeatsListCommand>();
        builder.RegisterCommand<ShowPollsCommand>();
        builder.RegisterCommand<PollSuggestCommand>();
        builder.RegisterCommand<DeletePollSuggestCommand>();
        builder.RegisterCommand<PollSuggestListCommand>();
        builder.RegisterCommand<BanPollCommand>();
        builder.RegisterCommand<UnbanPollCommand>();
        builder.RegisterCommand<MakeLogRoomCommand>();
        builder.RegisterCommand<DisableLogRoomCommand>();
        builder.RegisterCommand<ActivityHeatmapCommand>();
        builder.RegisterCommand<DayLineCountCommand>();
        builder.RegisterCommand<LinecountCommand>();
        builder.RegisterCommand<TopUsersCommand>();
        builder.RegisterCommand<MarkovCommand>();
        builder.RegisterCommand<MarkovStartCommand>();

        builder.RegisterHandler<JoinPhraseHandler>();
        builder.RegisterHandler<PollEndHandler>();
        builder.RegisterHandler<ChatLogHandler>();

        builder.RegisterType<MoneyService>().As<IMoneyService>().SingleInstance();
        builder.RegisterType<AddedCommandsManager>().As<IAddedCommandsManager>().As<IDynamicCommandProvider>()
            .SingleInstance();
        builder.RegisterType<RepeatsManager>().As<IRepeatsManager>().As<IBotLifecycleParticipant>().SingleInstance();
        builder.RegisterType<EventAnnouncer>().As<IEventAnnouncer>().SingleInstance();
        builder.RegisterType<ChatLogService>().As<IChatLogService>().As<IBotLifecycleParticipant>().SingleInstance();

        RegisterShopCommands(builder);
    }

    private static void RegisterShopCommands(ContainerBuilder builder)
    {
        builder.RegisterType<ShopService>().As<IShopService>().SingleInstance();
        builder.RegisterCommand<DisplayShopCommand>();
        builder.RegisterCommand<EditShopCommand>();
        builder.RegisterCommand<EditItemCommand>();
        builder.RegisterCommand<AddItemCommand>();
        builder.RegisterCommand<RemoveItemCommand>();
    }
}
