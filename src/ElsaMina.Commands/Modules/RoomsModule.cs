using Autofac;
using ElsaMina.Commands.ChatLog;
using ElsaMina.Commands.CustomCommands;
using ElsaMina.Commands.Development;
using ElsaMina.Commands.Economy;
using ElsaMina.Commands.JoinPhrases;
using ElsaMina.Commands.Polls;
using ElsaMina.Commands.Polls.Suggestions;
using ElsaMina.Commands.Repeats;
using ElsaMina.Commands.Repeats.Form;
using ElsaMina.Commands.Repeats.List;
using ElsaMina.Commands.Shop;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class RoomsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

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
        builder.RegisterCommand<OldElsaCommand>();
        builder.RegisterCommand<OldElsaStartCommand>();

        builder.RegisterType<OldElsaModelService>().As<IOldElsaModelService>().SingleInstance();

        builder.RegisterHandler<JoinPhraseHandler>();
        builder.RegisterHandler<PollEndHandler>();
        builder.RegisterHandler<ChatLogHandler>();

        builder.RegisterType<MoneyService>().As<IMoneyService>().SingleInstance();
        builder.RegisterType<ChatLogService>().As<IChatLogService>().SingleInstance().OnActivating(e =>
        {
            e.Instance.Start();
        }).AutoActivate();

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
