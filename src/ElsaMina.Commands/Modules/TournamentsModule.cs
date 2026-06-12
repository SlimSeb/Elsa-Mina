using Autofac;
using ElsaMina.Commands.TourConfigurator;
using ElsaMina.Commands.Tournaments;
using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Commands.Tournaments.Betting.Leaderboard;
using ElsaMina.Commands.Tournaments.Handlers;
using ElsaMina.Commands.Tournaments.Hebdo;
using ElsaMina.Commands.Tournaments.History;
using ElsaMina.Commands.Tournaments.Leaderboard;
using ElsaMina.Commands.Tournaments.Trade;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class TournamentsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<TopTournamentPlayersCommand>();
        builder.RegisterCommand<TourHistoryCommand>();
        builder.RegisterCommand<RandomTournamentCommand>();
        builder.RegisterCommand<BetCommand>();
        builder.RegisterCommand<CancelBetCommand>();
        builder.RegisterCommand<TopBettorsCommand>();
        builder.RegisterCommand<ConfigTourCommand>();
        builder.RegisterCommand<SaveTourCommand>();
        builder.RegisterCommand<EditTourFormCommand>();
        builder.RegisterCommand<DeleteTourCommand>();
        builder.RegisterCommand<LaunchTourCommand>();
        builder.RegisterCommand<TourPointsCommand>();
        builder.RegisterCommand<TradePointsCommand>();
        builder.RegisterCommand<RequestTradeCommand>();
        builder.RegisterCommand<NoTradeCommand>();

        builder.RegisterHandler<DisplayTeamsOnTourHandler>();
        builder.RegisterHandler<TourFinaleAnnounceHandler>();
        builder.RegisterHandler<OtherRoomTournamentAnnounceHandler>();
        builder.RegisterHandler<TournamentBettingHandler>();
        builder.RegisterHandler<TourEndHandler>();

        builder.RegisterType<TournamentBettingService>().As<ITournamentBettingService>().SingleInstance();
        builder.RegisterType<TourConfigService>().As<ITourConfigService>().SingleInstance();
        builder.RegisterType<TourConfigLauncher>().As<IDynamicCommandProvider>().SingleInstance();

        RegisterHebdoCommands(builder);
    }

    private static void RegisterHebdoCommands(ContainerBuilder builder)
    {
        builder.RegisterCommand<SharedPowerCommand>();
        builder.RegisterCommand<TourHelpCommand>();
        builder.RegisterCommand<HebdoSvCommand>();
        builder.RegisterCommand<HebdoSsCommand>();
        builder.RegisterCommand<HebdoSmCommand>();
        builder.RegisterCommand<HebdoAaaCommand>();
        builder.RegisterCommand<HebdoBhCommand>();
        builder.RegisterCommand<HebdoMnMCommand>();
        builder.RegisterCommand<HebdoGgCommand>();
        builder.RegisterCommand<HebdoStabCommand>();
        builder.RegisterCommand<HebdoPiCCommand>();
        builder.RegisterCommand<HebdoInheCommand>();
        builder.RegisterCommand<HebdoCamoCommand>();
        builder.RegisterCommand<HebdoNfeCommand>();
        builder.RegisterCommand<Hebdo1V1Command>();
        builder.RegisterCommand<HebdoAgCommand>();
        builder.RegisterCommand<HebdoLcuuCommand>();
        builder.RegisterCommand<HebdoUbersUuCommand>();
        builder.RegisterCommand<HebdoZuCommand>();
        builder.RegisterCommand<HebdoAdvruCommand>();
        builder.RegisterCommand<HebdoBwruCommand>();
        builder.RegisterCommand<HebdoOrasruCommand>();
        builder.RegisterCommand<HebdoSmruCommand>();
        builder.RegisterCommand<HebdoSsruCommand>();
    }
}
