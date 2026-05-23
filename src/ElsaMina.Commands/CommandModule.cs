using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Autofac;
using ElsaMina.Commands.Ai.Chat;
using ElsaMina.Commands.Ai.LanguageModel;
using ElsaMina.Commands.Ai.LanguageModel.Google;
using ElsaMina.Commands.Ai.LanguageModel.Mistral;
using ElsaMina.Commands.Ai.LanguageModel.OpenAi;
using ElsaMina.Commands.Ai.TextToSpeech;
using ElsaMina.Commands.Arcade.Caa;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Arcade.Inscriptions;
using ElsaMina.Commands.Arcade.Levels;
using ElsaMina.Commands.Arcade.Points;
using ElsaMina.Commands.Arcade.Sheets;
using ElsaMina.Commands.Arcade.Slots;
using ElsaMina.Commands.Badges;
using ElsaMina.Commands.Badges.BadgeDisplay;
using ElsaMina.Commands.Badges.BadgeEditPanel;
using ElsaMina.Commands.Badges.BadgeHolders;
using ElsaMina.Commands.Badges.BadgeList;
using ElsaMina.Commands.Badges.HallOfFame;
using ElsaMina.Commands.Badges.UserBadgePanel;
using ElsaMina.Commands.ChatLog;
using ElsaMina.Commands.CustomCommands;
using ElsaMina.Commands.Development;
using ElsaMina.Commands.Development.Commands;
using ElsaMina.Commands.Development.LagTest;
using ElsaMina.Commands.Games.Blackjack;
using ElsaMina.Commands.Games.ConnectFour;
using ElsaMina.Commands.Games.FloodIt;
using ElsaMina.Commands.Games.GuessingGame;
using ElsaMina.Commands.Games.GuessingGame.Capitals;
using ElsaMina.Commands.Games.GuessingGame.Countries;
using ElsaMina.Commands.Games.GuessingGame.Gatekeepers;
using ElsaMina.Commands.Games.GuessingGame.PokeCries;
using ElsaMina.Commands.Games.GuessingGame.PokeDesc;
using ElsaMina.Commands.Games.LightsOut;
using ElsaMina.Commands.Games.PokeRace;
using ElsaMina.Commands.Games.RockPaperScissors;
using ElsaMina.Commands.Games.TwentyFortyEight;
using ElsaMina.Commands.Games.VoltorbFlip;
using ElsaMina.Commands.JoinPhrases;
using ElsaMina.Commands.Misc;
using ElsaMina.Commands.Misc.Crypto;
using ElsaMina.Commands.Misc.Dailymotion;
using ElsaMina.Commands.Misc.Dictionary;
using ElsaMina.Commands.Misc.Facts;
using ElsaMina.Commands.Misc.Food;
using ElsaMina.Commands.Misc.Genius;
using ElsaMina.Commands.Misc.Help;
using ElsaMina.Commands.Misc.LeagueOfLegends;
using ElsaMina.Commands.Misc.Legacy;
using ElsaMina.Commands.Misc.Pokemon;
using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Commands.Misc.Translation;
using ElsaMina.Commands.Misc.UrlPreview;
using ElsaMina.Commands.Misc.Wiki;
using ElsaMina.Commands.Misc.Youtube;
using ElsaMina.Commands.Polls;
using ElsaMina.Commands.Polls.Suggestions;
using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Commands.Repeats;
using ElsaMina.Commands.Repeats.Form;
using ElsaMina.Commands.Repeats.List;
using ElsaMina.Commands.Replays;
using ElsaMina.Commands.RoomDashboard;
using ElsaMina.Commands.Shop;
using ElsaMina.Commands.Showdown;
using ElsaMina.Commands.Showdown.BattleTracker;
using ElsaMina.Commands.Showdown.Ladder;
using ElsaMina.Commands.Showdown.Ladder.EloHistory;
using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Commands.Showdown.SmogonStats;
using ElsaMina.Commands.Teams.Samples;
using ElsaMina.Commands.Teams.TeamPreviewOnLink;
using ElsaMina.Commands.Teams.TeamProviders;
using ElsaMina.Commands.Teams.TeamProviders.CoupCritique;
using ElsaMina.Commands.Teams.TeamProviders.Pokepaste;
using ElsaMina.Commands.Teams.TeamProviders.Showdown;
using ElsaMina.Commands.TourConfigurator;
using ElsaMina.Commands.Tournaments;
using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Commands.Tournaments.Handlers;
using ElsaMina.Commands.Tournaments.Hebdo;
using ElsaMina.Commands.Tournaments.History;
using ElsaMina.Commands.Tournaments.Leaderboard;
using ElsaMina.Commands.Tournaments.Trade;
using ElsaMina.Commands.Users;
using ElsaMina.Commands.Users.Colors;
using ElsaMina.Commands.Users.PlayTimes;
using ElsaMina.Commands.Users.Streaks;
using ElsaMina.Commands.Watchlist;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Utils;
using Assembly = System.Reflection.Assembly;

namespace ElsaMina.Commands;

public partial class CommandModule : Module
{
    private static readonly Regex FEATURE_RESOURCE_PATTERN =
        FeatureResourcePatternRegex();

    private static IEnumerable<ResourceManager> DiscoverFeatureResources()
    {
        var probeCulture = new CultureInfo("en-US");
        var seen = new HashSet<(string BaseName, Assembly Assembly)>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.FullName?.StartsWith("ElsaMina.") != true)
            {
                continue;
            }
            Assembly satelliteAssembly;
            try
            {
                satelliteAssembly = assembly.GetSatelliteAssembly(probeCulture);
            }
            catch
            {
                continue;
            }
            foreach (var resourceName in satelliteAssembly.GetManifestResourceNames())
            {
                var match = FEATURE_RESOURCE_PATTERN.Match(resourceName);
                if (match.Success)
                {
                    seen.Add((match.Groups[1].Value, assembly));
                }
            }
        }
        return seen.Select(item => new ResourceManager(item.BaseName, item.Assembly));
    }

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        foreach (var resourceManager in DiscoverFeatureResources())
        {
            builder.RegisterInstance(resourceManager).As<ResourceManager>().SingleInstance();
        }

#if DEBUG
        builder.RegisterCommand<ScriptCommand>();
#endif
        builder.RegisterCommand<Ping>();
        builder.RegisterCommand<LagTestCommand>();
        builder.RegisterCommand<AddCustomCommand>();
        builder.RegisterCommand<CustomCommandList>();
        builder.RegisterCommand<AddBadgeCommand>();
        builder.RegisterCommand<BadgeEditPanelCommand>();
        builder.RegisterCommand<UserBadgePanelCommand>();
        builder.RegisterCommand<EditBadgeCommand>();
        builder.RegisterCommand<SetLocaleCommand>();
        builder.RegisterCommand<HelpCommand>();
        builder.RegisterCommand<ShowRoomDashboard>();
        builder.RegisterCommand<RoomConfigCommand>();
        builder.RegisterCommand<KillCommand>();
        builder.RegisterCommand<StopConnectionCommand>();
        builder.RegisterCommand<DeleteCustomCommand>();
        builder.RegisterCommand<EditCustomCommand>();
        builder.RegisterCommand<TemplatesDebugCommand>();
        builder.RegisterCommand<GiveBadgeCommand>();
        builder.RegisterCommand<ProfileCommand>();
        builder.RegisterCommand<SetEmojiCommand>();
        builder.RegisterCommand<SetProfileColorCommand>();
        builder.RegisterCommand<EditProfilePanelCommand>();
        builder.RegisterCommand<DeleteBadgeCommand>();
        builder.RegisterCommand<TakeBadgeCommand>();
        builder.RegisterCommand<SetAvatarCommand>();
        builder.RegisterCommand<SetTitleCommand>();
        builder.RegisterCommand<GetAllCommand>();
        builder.RegisterCommand<GuessingGameCommand>();
        builder.RegisterCommand<EndGuessingGameCommand>();
        builder.RegisterCommand<AddTeamCommand>();
        builder.RegisterCommand<AddTeamToRoomCommand>();
        builder.RegisterCommand<TeamShowcaseCommand>();
        builder.RegisterCommand<TeamListCommand>();
        builder.RegisterCommand<DeleteTeamCommand>();
        builder.RegisterCommand<DeleteAllTeamsByTierCommand>();
        builder.RegisterCommand<SayCommand>();
        builder.RegisterCommand<NameColorInfoCommand>();
        builder.RegisterCommand<SetColorCommand>();
        builder.RegisterCommand<RemoveColorCommand>();
        builder.RegisterCommand<FactsCommand>();
        builder.RegisterCommand<BitcoinCommand>();
        builder.RegisterCommand<DogecoinCommand>();
        builder.RegisterCommand<EthereumCommand>();
        builder.RegisterCommand<LeagueRankCommand>();
        builder.RegisterCommand<LeagueOfLegendsHistoryCommand>();
        builder.RegisterCommand<RandRecipeCommand>();
        builder.RegisterCommand<RecipeSearchCommand>();
        builder.RegisterCommand<RandPastaCommand>();
        builder.RegisterCommand<RandSoupCommand>();
        builder.RegisterCommand<RandCheeseCommand>();
        builder.RegisterCommand<RandSaladCommand>();
        builder.RegisterCommand<PizzaCommand>();
        builder.RegisterCommand<SaladCommand>();
        builder.RegisterCommand<SetJoinPhraseCommand>();
        builder.RegisterCommand<BlackjackCommand>();
        builder.RegisterCommand<BlackjackJoinCommand>();
        builder.RegisterCommand<BlackjackHitCommand>();
        builder.RegisterCommand<BlackjackStandCommand>();
        builder.RegisterCommand<BlackjackEndCommand>();
        builder.RegisterType<BlackjackGame>().AsSelf();
        builder.RegisterType<BlackjackGameManager>().As<IBlackjackGameManager>().SingleInstance();
        builder.RegisterCommand<CreateConnectFourCommand>();
        builder.RegisterCommand<JoinConnectFourCommand>();
        builder.RegisterCommand<PlayConnectFourCommand>();
        builder.RegisterCommand<EndConnectFourCommand>();
        builder.RegisterCommand<YoutubeCommand>();
        builder.RegisterCommand<DictionaryCommand>();
        builder.RegisterCommand<BugReportCommand>();
        builder.RegisterCommand<SetArcadeLevelCommand>();
        builder.RegisterCommand<DisplayArcadeLevelsCommand>();
        builder.RegisterCommand<DeleteArcadeLevelCommand>();
        builder.RegisterCommand<GetArcadeLevelCommand>();
        builder.RegisterCommand<AddPointsCommand>();
        builder.RegisterCommand<RemovePointsCommand>();
        builder.RegisterCommand<LeaderboardCommand>();
        builder.RegisterCommand<ClearPointsCommand>();
        builder.RegisterCommand<ForfeitConnectFourCommand>();
        builder.RegisterCommand<ConnectFourLeaderboardCommand>();
        builder.RegisterCommand<ConnectFourEloCommand>();
        builder.RegisterCommand<AskElsaCommand>();
        builder.RegisterCommand<SpeakCommand>();
        builder.RegisterCommand<RankingCommand>();
        builder.RegisterCommand<SmogonStatsCommand>();
        builder.RegisterCommand<LadderCommand>();
        builder.RegisterCommand<ShowAvatarCommand>();
        builder.RegisterCommand<ToggleLadderTrackerCommand>();
        builder.RegisterCommand<FailCommand>();
        builder.RegisterCommand<RepeatFormCommand>();
        builder.RegisterCommand<StartRepeatCommand>();
        builder.RegisterCommand<StopRepeatCommand>();
        builder.RegisterCommand<RepeatsListCommand>();
        builder.RegisterCommand<WikipediaSearchCommand>();
        builder.RegisterCommand<PokepediaSearchCommand>();
        builder.RegisterCommand<BulbapediaSearchCommand>();
        builder.RegisterCommand<AfdSpriteCommand>();
        builder.RegisterCommand<PokemonTranslateCommand>();
        builder.RegisterCommand<GoogleTranslateCommand>();
        builder.RegisterCommand<BadTranslateCommand>();
        builder.RegisterCommand<FullPotCommand>();
        builder.RegisterCommand<ShowPollsCommand>();
        builder.RegisterCommand<PairingsCommand>();
        builder.RegisterCommand<EvroMakerCommand>();
        builder.RegisterCommand<DebilifyCommand>();
        builder.RegisterCommand<WeebifyCommand>();
        builder.RegisterCommand<ElectionCommand>();
        builder.RegisterCommand<DailymotionCommand>();
        builder.RegisterCommand<ChangelogCommand>();
        builder.RegisterCommand<MemoryUsageCommand>();
        builder.RegisterCommand<UptimeCommand>();
        builder.RegisterCommand<BadgeDisplayCommand>();
        builder.RegisterCommand<BadgeHoldersCommand>();
        builder.RegisterCommand<BadgeListCommand>();
        builder.RegisterCommand<HallOfFameCommand>();
        builder.RegisterCommand<GeniusSearchCommand>();
        builder.RegisterCommand<ArcadeHallOfFameCommand>();
        builder.RegisterCommand<ArcadeSheetAddPointsCommand>();
        builder.RegisterCommand<AddCaaPointsCommand>();
        builder.RegisterCommand<CaaCommand>();
        builder.RegisterCommand<ArcadeInCommand>();
        builder.RegisterCommand<ArcadeLeaveCommand>();
        builder.RegisterCommand<ArcadeStartCommand>();
        builder.RegisterCommand<ArcadeStopCommand>();
        builder.RegisterCommand<ArcadeListCommand>();
        builder.RegisterCommand<ArcadeRemoveCommand>();
        builder.RegisterCommand<ArcadeTimerCommand>();
        builder.RegisterCommand<ArcadeAddCommand>();
        builder.RegisterCommand<SlotsFunCommand>();
        builder.RegisterCommand<TimerCommand>();
        builder.RegisterCommand<ShipCommand>();
        builder.RegisterCommand<RunningCommands>();
        builder.RegisterCommand<CancelRunningCommand>();
        builder.RegisterCommand<SeenCommand>();
        builder.RegisterCommand<AltsCommand>();
        builder.RegisterCommand<TopPlayTimesCommand>();
        builder.RegisterCommand<PlayTimeCommand>();
        builder.RegisterCommand<StreakCommand>();
        builder.RegisterCommand<StreakLeaderboardCommand>();
        builder.RegisterCommand<CurrentLadderTrackersCommand>();
        builder.RegisterCommand<GuessingGameAnswerCommand>();
        builder.RegisterCommand<TopTournamentPlayersCommand>();
        builder.RegisterCommand<TourHistoryCommand>();
        builder.RegisterCommand<RandomTournamentCommand>();
        builder.RegisterCommand<ArcadeSheetAddPointsCommand>();
        builder.RegisterCommand<ArcadePointsCommand>();
        builder.RegisterCommand<StartVoltorbFlipCommand>();
        builder.RegisterCommand<JoinVoltorbFlipCommand>();
        builder.RegisterCommand<FlipVoltorbFlipCommand>();
        builder.RegisterCommand<ToggleMarkVoltorbFlipCommand>();
        builder.RegisterCommand<QuitVoltorbFlipCommand>();
        builder.RegisterCommand<EndVoltorbFlipCommand>();
        builder.RegisterCommand<StartPokeRaceCommand>();
        builder.RegisterCommand<JoinPokeRaceCommand>();
        builder.RegisterCommand<StartRaceCommand>();
        builder.RegisterCommand<EndRaceCommand>();
        builder.RegisterCommand<VoltorbFlipLeaderboardCommand>();
        builder.RegisterCommand<RandomCustomCommand>();
        builder.RegisterCommand<MakeLogRoomCommand>();
        builder.RegisterCommand<DisableLogRoomCommand>();
        builder.RegisterCommand<DayLineCountCommand>();
        builder.RegisterCommand<LinecountCommand>();
        builder.RegisterCommand<TopUsersCommand>();

        RegisterRandomImagesCommands(builder);
        RegisterTournamentCommands(builder);
        RegisterShopCommands(builder);

        builder.RegisterHandler<LagTestHandler>();
        builder.RegisterHandler<JoinRoomOnInviteHandler>();
        builder.RegisterHandler<GuessingGameHandler>();
        builder.RegisterHandler<DisplayTeamOnLinkHandler>();
        builder.RegisterHandler<JoinPhraseHandler>();
        builder.RegisterHandler<ReplaysHandler>();
        builder.RegisterHandler<YoutubeVideoOnLinkHandler>();
        builder.RegisterHandler<UrlPreviewHandler>();
        builder.RegisterHandler<StreakUpdateHandler>();
        builder.RegisterHandler<DisplayTeamsOnTourHandler>();
        builder.RegisterHandler<TourFinaleAnnounceHandler>();
        builder.RegisterHandler<ArcadeEventsHandler>();
        builder.RegisterHandler<PollEndHandler>();
        builder.RegisterHandler<OtherRoomTournamentAnnounceHandler>();
        builder.RegisterHandler<TournamentBettingHandler>();
        builder.RegisterType<TournamentBettingService>().As<ITournamentBettingService>().SingleInstance();
        builder.RegisterCommand<BetCommand>();
        builder.RegisterCommand<CancelBetCommand>();
        builder.RegisterHandler<TourEndHandler>();
        builder.RegisterHandler<StaffIntroChangeHandler>();
        builder.RegisterHandler<StaffIntroContentHandler>();

        builder.RegisterCommand<AddWatchlistCommand>();
        builder.RegisterCommand<RemoveWatchlistCommand>();

        builder.RegisterCommand<ConfigTourCommand>();
        builder.RegisterCommand<SaveTourCommand>();
        builder.RegisterCommand<EditTourFormCommand>();
        builder.RegisterCommand<DeleteTourCommand>();
        builder.RegisterCommand<LaunchTourCommand>();

        builder.RegisterCommand<ConfigEventRolesCommand>();
        builder.RegisterCommand<SaveEventRoleCommand>();
        builder.RegisterCommand<DeleteEventRoleCommand>();
        builder.RegisterCommand<MuteGamesCommand>();
        builder.RegisterCommand<UnmuteGamesCommand>();

        builder.RegisterCommand<PollSuggestCommand>();
        builder.RegisterCommand<DeletePollSuggestCommand>();
        builder.RegisterCommand<PollSuggestListCommand>();
        builder.RegisterCommand<BanPollCommand>();
        builder.RegisterCommand<UnbanPollCommand>();

        builder.RegisterCommand<TourPointsCommand>();
        builder.RegisterCommand<TradePointsCommand>();
        builder.RegisterCommand<RequestTradeCommand>();
        builder.RegisterCommand<NoTradeCommand>();

        builder.RegisterCommand<StartLightsOutCommand>();
        builder.RegisterCommand<JoinLightsOutCommand>();
        builder.RegisterCommand<ToggleLightsOutCommand>();
        builder.RegisterCommand<EndLightsOutCommand>();
        builder.RegisterCommand<LightsOutLeaderboardCommand>();

        builder.RegisterCommand<StartFloodItCommand>();
        builder.RegisterCommand<JoinFloodItCommand>();
        builder.RegisterCommand<FloodItColorCommand>();
        builder.RegisterCommand<EndFloodItCommand>();
        builder.RegisterCommand<FloodItLeaderboardCommand>();

        builder.RegisterCommand<StartRpsCommand>();
        builder.RegisterCommand<JoinRpsCommand>();
        builder.RegisterCommand<RockRpsCommand>();
        builder.RegisterCommand<PaperRpsCommand>();
        builder.RegisterCommand<ScissorsRpsCommand>();
        builder.RegisterCommand<EndRpsCommand>();

        builder.RegisterCommand<StartTwentyFortyEightCommand>();
        builder.RegisterCommand<JoinTwentyFortyEightCommand>();
        builder.RegisterCommand<MoveTwentyFortyEightCommand>();
        builder.RegisterCommand<EndTwentyFortyEightCommand>();
        builder.RegisterCommand<TwentyFortyEightLeaderboardCommand>();

        builder.RegisterType<CountriesGame>().AsSelf();
        builder.RegisterType<CapitalCitiesGame>().AsSelf();
        builder.RegisterType<ConnectFourGame>().AsSelf();
        builder.RegisterType<ConnectFourRatingService>().As<IConnectFourRatingService>().SingleInstance();
        builder.RegisterType<VoltorbFlipGame>().AsSelf();
        builder.RegisterType<PokeRaceGame>().AsSelf();
        builder.RegisterType<RpsGame>().AsSelf();
        builder.RegisterType<PokeDescGame>().AsSelf();
        builder.RegisterType<PokeCriesGame>().AsSelf();
        builder.RegisterType<GatekeepersGame>().AsSelf();

        builder.RegisterType<LightsOutGame>().AsSelf();
        builder.RegisterType<LightsOutGameManager>().As<ILightsOutGameManager>().SingleInstance();
        builder.RegisterType<FloodItGame>().AsSelf();
        builder.RegisterType<FloodItGameManager>().As<IFloodItGameManager>().SingleInstance();
        builder.RegisterType<TwentyFortyEightGame>().AsSelf();
        builder.RegisterType<TwentyFortyEightGameManager>().As<ITwentyFortyEightGameManager>().SingleInstance();
        builder.RegisterType<VoltorbFlipGameManager>().As<IVoltorbFlipGameManager>().SingleInstance();
        builder.RegisterType<EventRoleMappingService>().As<IEventRoleMappingService>().SingleInstance();
        builder.RegisterType<TourConfigService>().As<ITourConfigService>().SingleInstance();
        builder.RegisterType<TourConfigLauncher>().As<IDynamicCommandProvider>().SingleInstance();
        builder.RegisterType<WatchlistService>().As<IWatchlistService>().SingleInstance();
        builder.RegisterType<NameColorsService>().As<INameColorsService>().As<IRoomColorsCache>().SingleInstance();
        builder.RegisterType<ElevenLabsAiTextToSpeechProvider>().As<IAiTextToSpeechProvider>().SingleInstance();
        builder.RegisterType<Gemini25FlashProvider>().AsSelf().SingleInstance();
        builder.RegisterType<MistralMediumProvider>().AsSelf().SingleInstance();
        builder.RegisterType<GptNano41Provider>().AsSelf().SingleInstance();
        builder.RegisterType<ConversationHistoryService>().As<IConversationHistoryService>().SingleInstance();
        builder.RegisterType<LanguageModelResolver>().As<ILanguageModelProvider>().SingleInstance();
        builder.RegisterType<ProfileService>().As<IProfileService>().SingleInstance();
        builder.RegisterType<StreakService>().As<IStreakService>().SingleInstance();
        builder.RegisterType<ShowdownRanksProvider>().As<IShowdownRanksProvider>().SingleInstance();
        builder.RegisterType<SmogonUsageDataProvider>().As<ISmogonUsageDataProvider>().SingleInstance();
        builder.RegisterType<LadderHistoryManager>().As<ILadderHistoryManager>().SingleInstance();
        builder.RegisterType<LadderTrackerManager>().As<ILadderTrackerManager>().SingleInstance();
        builder.RegisterType<EloProgressionManager>().As<IEloProgressionManager>().SingleInstance().OnActivating(e =>
        {
            e.Instance.InitializeAsync().Wait();
        }).AutoActivate();
        builder.RegisterType<PokepasteProvider>().As<ITeamProvider>().SingleInstance();
        builder.RegisterType<CoupCritiqueProvider>().As<ITeamProvider>().SingleInstance();
        builder.RegisterType<ShowdownTeamProvider>().As<ITeamProvider>().SingleInstance();
        builder.RegisterType<TeamLinkMatchFactory>().As<ITeamLinkMatchFactory>().SingleInstance();
        builder.RegisterType<DataManager>().As<IDataManager>().SingleInstance().OnActivating(e =>
        {
            e.Instance.Initialize().Wait(); // Risque d'ANR mais obligé pour garantir la bonne initialisation...
        });
        builder.RegisterType<EloHistoryService>().As<IEloHistoryService>().SingleInstance().OnActivating(e =>
        {
            e.Instance.Start();
        }).AutoActivate();
        builder.RegisterType<ChatLogService>().As<IChatLogService>().SingleInstance().OnActivating(e =>
        {
            e.Instance.Start();
        }).AutoActivate();
        builder.RegisterHandler<ChatLogHandler>();
        builder.RegisterCommand<LadderGraphCommand>();
        builder.RegisterCommand<TrackEloProgressionCommand>();
        builder.RegisterCommand<UntrackEloProgressionCommand>();
        builder.RegisterCommand<ListTrackedEloProgressionsCommand>();
        builder.RegisterType<LagTestManager>().As<ILagTestManager>().SingleInstance();
        builder.RegisterType<ArcadeInscriptionsManager>().As<IArcadeInscriptionsManager>().SingleInstance();
        builder.RegisterType<ArcadeEventsService>().As<IArcadeEventsService>().SingleInstance();
        builder.RegisterType<UnsplashService>().As<IUnsplashService>().SingleInstance();
        builder.RegisterType<TenorService>().As<ITenorService>().SingleInstance();
        builder.RegisterType<SpoonacularService>().As<ISpoonacularService>().SingleInstance();
    }

    private static void RegisterTournamentCommands(ContainerBuilder builder)
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

    private static void RegisterShopCommands(ContainerBuilder builder)
    {
        builder.RegisterType<ShopService>().As<IShopService>().SingleInstance();
        builder.RegisterCommand<DisplayShopCommand>();
        builder.RegisterCommand<EditShopCommand>();
        builder.RegisterCommand<EditItemCommand>();
        builder.RegisterCommand<AddItemCommand>();
        builder.RegisterCommand<RemoveItemCommand>();
    }

    private static void RegisterRandomImagesCommands(ContainerBuilder builder)
    {
        builder.RegisterCommand<RandCatCommand>();
        builder.RegisterCommand<RandDogCommand>();
        builder.RegisterCommand<RandImageCommand>();
        builder.RegisterCommand<RandTurtleCommand>();
        builder.RegisterCommand<RandCapyCommand>();
        builder.RegisterCommand<RandGoatCommand>();
        builder.RegisterCommand<RandElephantCommand>();
        builder.RegisterCommand<RandPigCommand>();
        builder.RegisterCommand<RandBirdCommand>();
        builder.RegisterCommand<RandDolphinCommand>();
        builder.RegisterCommand<RandWolfCommand>();
        builder.RegisterCommand<RandTigerCommand>();
        builder.RegisterCommand<RandCheetahCommand>();
        builder.RegisterCommand<RandLionCommand>();
        builder.RegisterCommand<RandJaguarCommand>();
        builder.RegisterCommand<RandButterflyCommand>();
        builder.RegisterCommand<RandMouseCommand>();
        builder.RegisterCommand<RandMonkeyCommand>();
        builder.RegisterCommand<RandBearCommand>();
        builder.RegisterCommand<RandRabbitCommand>();
        builder.RegisterCommand<RandFrogCommand>();
        builder.RegisterCommand<RandSnakeCommand>();
        builder.RegisterCommand<RandSpiderCommand>();
        builder.RegisterCommand<RandSharkCommand>();
        builder.RegisterCommand<RandRacletteCommand>();
        builder.RegisterCommand<RandHeartGifCommand>();
        builder.RegisterCommand<RandCommand>();
        builder.RegisterCommand<RandGifCommand>();
        builder.RegisterCommand<RandMp4Command>();
        builder.RegisterCommand<TenorSearchCommand>();
        builder.RegisterCommand<TenorGifCommand>();
        builder.RegisterCommand<RandFurretCommand>();
        builder.RegisterCommand<WalkCommand>();
        builder.RegisterCommand<RandHelpCommand>();
    }

    [GeneratedRegex(@"^(.+\.Resources\.\w+?)\.[\w-]+\.resources$", RegexOptions.Compiled)]
    private static partial Regex FeatureResourcePatternRegex();
}