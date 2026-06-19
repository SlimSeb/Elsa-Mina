using Autofac;
using ElsaMina.Commands.Games;
using ElsaMina.Commands.Games.Battleship;
using ElsaMina.Commands.Games.Belote;
using ElsaMina.Commands.Games.Blackjack;
using ElsaMina.Commands.Games.Catalog;
using ElsaMina.Commands.Games.Chess;
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
using ElsaMina.Commands.Games.Poker;
using ElsaMina.Commands.Games.RockPaperScissors;
using ElsaMina.Commands.Games.Semantix;
using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Commands.Games.TwentyFortyEight;
using ElsaMina.Commands.Games.VoltorbFlip;
using ElsaMina.Commands.Games.Wordle;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class GamesModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<GuessingGameCommand>();
        builder.RegisterCommand<EndGuessingGameCommand>();
        builder.RegisterCommand<GuessingGameAnswerCommand>();

        builder.RegisterCommand<BlackjackCommand>();
        builder.RegisterCommand<BlackjackJoinCommand>();
        builder.RegisterCommand<BlackjackHitCommand>();
        builder.RegisterCommand<BlackjackStandCommand>();
        builder.RegisterCommand<BlackjackEndCommand>();

        builder.RegisterCommand<CreateConnectFourCommand>();
        builder.RegisterCommand<JoinConnectFourCommand>();
        builder.RegisterCommand<PlayConnectFourCommand>();
        builder.RegisterCommand<EndConnectFourCommand>();
        builder.RegisterCommand<ForfeitConnectFourCommand>();
        builder.RegisterCommand<ConnectFourLeaderboardCommand>();
        builder.RegisterCommand<ConnectFourEloCommand>();

        builder.RegisterCommand<CreateChessCommand>();
        builder.RegisterCommand<JoinChessCommand>();
        builder.RegisterCommand<PlayChessCommand>();
        builder.RegisterCommand<EndChessCommand>();
        builder.RegisterCommand<ForfeitChessCommand>();
        builder.RegisterCommand<ChessLeaderboardCommand>();
        builder.RegisterCommand<ChessEloCommand>();

        builder.RegisterCommand<CreateBattleshipCommand>();
        builder.RegisterCommand<JoinBattleshipCommand>();
        builder.RegisterCommand<PlaceBattleshipCommand>();
        builder.RegisterCommand<RotateBattleshipCommand>();
        builder.RegisterCommand<RandomBattleshipCommand>();
        builder.RegisterCommand<ResetBattleshipCommand>();
        builder.RegisterCommand<FireBattleshipCommand>();
        builder.RegisterCommand<EndBattleshipCommand>();
        builder.RegisterCommand<ForfeitBattleshipCommand>();
        builder.RegisterCommand<BattleshipLeaderboardCommand>();
        builder.RegisterCommand<BattleshipEloCommand>();

        builder.RegisterCommand<StartVoltorbFlipCommand>();
        builder.RegisterCommand<JoinVoltorbFlipCommand>();
        builder.RegisterCommand<FlipVoltorbFlipCommand>();
        builder.RegisterCommand<ToggleMarkVoltorbFlipCommand>();
        builder.RegisterCommand<QuitVoltorbFlipCommand>();
        builder.RegisterCommand<EndVoltorbFlipCommand>();
        builder.RegisterCommand<VoltorbFlipLeaderboardCommand>();

        builder.RegisterCommand<StartPokeRaceCommand>();
        builder.RegisterCommand<JoinPokeRaceCommand>();
        builder.RegisterCommand<StartRaceCommand>();
        builder.RegisterCommand<EndRaceCommand>();

        builder.RegisterCommand<StartLightsOutCommand>();
        builder.RegisterCommand<JoinLightsOutCommand>();
        builder.RegisterCommand<ToggleLightsOutCommand>();
        builder.RegisterCommand<EndLightsOutCommand>();
        builder.RegisterCommand<LightsOutLeaderboardCommand>();

        builder.RegisterCommand<StartWordleCommand>();
        builder.RegisterCommand<WordleKeyCommand>();
        builder.RegisterCommand<EndWordleCommand>();
        builder.RegisterCommand<WordleLeaderboardCommand>();

        builder.RegisterCommand<StartSemantixCommand>();
        builder.RegisterCommand<GuessSemantixCommand>();
        builder.RegisterCommand<EndSemantixCommand>();
        builder.RegisterCommand<SemantixLeaderboardCommand>();
        builder.RegisterCommand<SemantixAnswerCommand>();

        builder.RegisterCommand<GamesCommand>();

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

        builder.RegisterCommand<StartTarotCommand>();
        builder.RegisterCommand<JoinTarotCommand>();
        builder.RegisterCommand<BeginTarotCommand>();
        builder.RegisterCommand<BidTarotCommand>();
        builder.RegisterCommand<CallKingTarotCommand>();
        builder.RegisterCommand<DiscardTarotCommand>();
        builder.RegisterCommand<PlayTarotCommand>();
        builder.RegisterCommand<DeclarePoigneeTarotCommand>();
        builder.RegisterCommand<AnnounceSlamTarotCommand>();
        builder.RegisterCommand<ResendTarotCommand>();
        builder.RegisterCommand<RequestTarotSubCommand>();
        builder.RegisterCommand<AcceptTarotSubCommand>();
        builder.RegisterCommand<EndTarotCommand>();
        builder.RegisterCommand<TarotLeaderboardCommand>();
        builder.RegisterCommand<TarotStatsCommand>();

        builder.RegisterCommand<StartBeloteCommand>();
        builder.RegisterCommand<JoinBeloteCommand>();
        builder.RegisterCommand<BeginBeloteCommand>();
        builder.RegisterCommand<BidBeloteCommand>();
        builder.RegisterCommand<PlayBeloteCommand>();
        builder.RegisterCommand<ResendBeloteCommand>();
        builder.RegisterCommand<RequestBeloteSubCommand>();
        builder.RegisterCommand<AcceptBeloteSubCommand>();
        builder.RegisterCommand<EndBeloteCommand>();
        builder.RegisterCommand<BeloteLeaderboardCommand>();
        builder.RegisterCommand<BeloteStatsCommand>();

        builder.RegisterCommand<StartPokerCommand>();
        builder.RegisterCommand<JoinPokerCommand>();
        builder.RegisterCommand<BeginPokerCommand>();
        builder.RegisterCommand<FoldPokerCommand>();
        builder.RegisterCommand<CheckPokerCommand>();
        builder.RegisterCommand<CallPokerCommand>();
        builder.RegisterCommand<RaisePokerCommand>();
        builder.RegisterCommand<EndPokerCommand>();

        builder.RegisterHandler<GuessingGameHandler>();
        builder.RegisterHandler<HangmanAnnounceHandler>();

        builder.RegisterType<CountriesGame>().AsSelf();
        builder.RegisterType<CapitalCitiesGame>().AsSelf();
        builder.RegisterType<ConnectFourGame>().AsSelf();
        builder.RegisterType<ConnectFourRatingService>().As<IConnectFourRatingService>().SingleInstance();
        builder.RegisterType<ChessGame>().AsSelf();
        builder.RegisterType<ChessRatingService>().As<IChessRatingService>().SingleInstance();
        builder.RegisterType<BattleshipGame>().AsSelf();
        builder.RegisterType<BattleshipRatingService>().As<IBattleshipRatingService>().SingleInstance();
        builder.RegisterType<VoltorbFlipGame>().AsSelf();
        builder.RegisterType<PokeRaceGame>().AsSelf();
        builder.RegisterType<RpsGame>().AsSelf();
        builder.RegisterType<TarotGame>().AsSelf();
        builder.RegisterType<TarotStatsService>().As<ITarotStatsService>().SingleInstance();
        builder.RegisterType<BeloteGame>().AsSelf();
        builder.RegisterType<BeloteStatsService>().As<IBeloteStatsService>().SingleInstance();
        builder.RegisterType<PokerGame>().AsSelf();
        builder.RegisterType<PokeDescGame>().AsSelf();
        builder.RegisterType<PokeCriesGame>().AsSelf();
        builder.RegisterType<GatekeepersGame>().AsSelf();
        builder.RegisterType<LightsOutGame>().AsSelf();
        builder.RegisterType<LightsOutGameManager>().As<ILightsOutGameManager>().SingleInstance();
        builder.RegisterType<WordleGame>().AsSelf();
        builder.RegisterType<WordleGameManager>().As<IWordleGameManager>().SingleInstance();
        builder.RegisterType<WordleDailyService>().As<IWordleDailyService>().SingleInstance();
        builder.RegisterType<SemantixGame>().AsSelf();
        builder.RegisterType<SemantixGameManager>().As<ISemantixGameManager>().SingleInstance();
        builder.RegisterType<SemantixDailyService>().As<ISemantixDailyService>().SingleInstance();
        builder.RegisterType<Word2VecEmbeddingService>().As<IEmbeddingService>().SingleInstance();
        builder.RegisterType<FloodItGame>().AsSelf();
        builder.RegisterType<FloodItGameManager>().As<IFloodItGameManager>().SingleInstance();
        builder.RegisterType<TwentyFortyEightGame>().AsSelf();
        builder.RegisterType<TwentyFortyEightGameManager>().As<ITwentyFortyEightGameManager>().SingleInstance();
        builder.RegisterType<VoltorbFlipGameManager>().As<IVoltorbFlipGameManager>().SingleInstance();
        builder.RegisterType<BlackjackGame>().AsSelf();
        builder.RegisterType<BlackjackGameManager>().As<IBlackjackGameManager>().SingleInstance();
    }
}
