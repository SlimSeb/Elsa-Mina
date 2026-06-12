using Autofac;
using ElsaMina.Commands.Misc;
using ElsaMina.Commands.Misc.BugReport;
using ElsaMina.Commands.Misc.Crypto;
using ElsaMina.Commands.Misc.Dailymotion;
using ElsaMina.Commands.Misc.Dictionary;
using ElsaMina.Commands.Misc.Facts;
using ElsaMina.Commands.Misc.Food;
using ElsaMina.Commands.Misc.Genius;
using ElsaMina.Commands.Misc.LeagueOfLegends;
using ElsaMina.Commands.Misc.Legacy;
using ElsaMina.Commands.Misc.Pokemon;
using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Commands.Misc.Translation;
using ElsaMina.Commands.Misc.UrlPreview;
using ElsaMina.Commands.Misc.Wiki;
using ElsaMina.Commands.Misc.Youtube;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class MiscModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

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
        builder.RegisterCommand<YoutubeCommand>();
        builder.RegisterCommand<DictionaryCommand>();
        builder.RegisterCommand<BugReportCommand>();
        builder.RegisterCommand<SubmitBugReportCommand>();
        builder.RegisterCommand<WikipediaSearchCommand>();
        builder.RegisterCommand<PokepediaSearchCommand>();
        builder.RegisterCommand<BulbapediaSearchCommand>();
        builder.RegisterCommand<AfdSpriteCommand>();
        builder.RegisterCommand<PokemonTranslateCommand>();
        builder.RegisterCommand<GoogleTranslateCommand>();
        builder.RegisterCommand<BadTranslateCommand>();
        builder.RegisterCommand<FullPotCommand>();
        builder.RegisterCommand<PairingsCommand>();
        builder.RegisterCommand<EvroMakerCommand>();
        builder.RegisterCommand<DebilifyCommand>();
        builder.RegisterCommand<WeebifyCommand>();
        builder.RegisterCommand<ElectionCommand>();
        builder.RegisterCommand<DailymotionCommand>();
        builder.RegisterCommand<GeniusSearchCommand>();
        builder.RegisterCommand<TimerCommand>();
        builder.RegisterCommand<ShipCommand>();

        builder.RegisterHandler<YoutubeVideoOnLinkHandler>();
        builder.RegisterHandler<UrlPreviewHandler>();

        builder.RegisterType<GithubIssueService>().As<IGithubIssueService>().SingleInstance();
        builder.RegisterType<UnsplashService>().As<IUnsplashService>().SingleInstance();
        builder.RegisterType<TenorService>().As<ITenorService>().SingleInstance();
        builder.RegisterType<TenorCooldownService>().As<ITenorCooldownService>().SingleInstance();
        builder.RegisterType<SpoonacularService>().As<ISpoonacularService>().SingleInstance();

        RegisterRandomImagesCommands(builder);
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
}
