using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Tournaments;

[NamedCommand("randtour", Aliases = ["randomtournament"])]
public class RandomTournamentCommand : Command
{
    private const string RANDOM_BATTLE = "randombattle";
    private const string RANDOM_BATTLE_MAYHEM = "randombattlemayhem";

    private static readonly RandomTournamentEntry[] TOURNAMENTS =
    [
        new("Random Battle Shared Power", RANDOM_BATTLE_MAYHEM, "!scalemonsmod,!camomonsmod,!inversemod", "sp", "", ""),
        new("Random Battle Monotype", "monotyperandombattle", "", "", "", ""),
        new("Random Super Metronome", "superstaffbrosultimate", "dynamaxclause", "super metronome", "", ""),
        new("Battle Factory Shared Power", "battlefactory", "mayhem,!scalemonsmod,!camomonsmod,!inversemod", "spnl", "",
            ""),
        new("Random Battle 1v1 Bo3", RANDOM_BATTLE, "maxteamsize=3,pickedteamsize=1,bestof=3", "1v1", "team preview",
            ""),
        new("[Gen 8] Random CAP 6v6", "gen8cap1v1", "!!maxteamsize=6,!!pickedteamsize=6", "cap", "", ""),
        new("Broken Cup Shared Power", "brokencup", "mayhem,!scalemonsmod,!camomonsmod,!inversemod", "spnl", "", ""),
        new("[Gen 7] Random Protean Shared", "gen7randombattle",
            "proteanpalacemod,mayhem,!scalemonsmod,!camomonsmod,!inversemod", "spnl", "protean", ""),
        new("Random Bonus Type Revelationmons", RANDOM_BATTLE, "bonustypemod,revelationmonsmod", "revelationmons",
            "bt", ""),
        new("Random Battle Camomons", RANDOM_BATTLE, "camomonsmod", "camo", "", ""),
        new("Random Battle Inverse", RANDOM_BATTLE, "inversemod", "inverse", "", ""),
        new("Baby Random Battle", "babyrandombattle", "", "babyrandombattle", "", ""),
        new("Random Battle Protean Palace", RANDOM_BATTLE, "proteanpalacemod", "proteanpalace", "", ""),
        new("Random Battle First Blood Bo3", RANDOM_BATTLE, "firstbloodrule,bestof=3", "firstblood", "", ""),
        new("Random Battle Mort Subite", RANDOM_BATTLE_MAYHEM,
            "firstbloodrule,maxteamsize=24,!inversemod,!scalemonsmod,!camomonsmod,proteanpalacemod", "firstblood", "sp",
            "proteanpalace"),
        new("Random Battle Trop d'Attaques", RANDOM_BATTLE, "maxmovecount=6,forceofthefallenmod", "mmc6", "fotf", ""),
        new("Random Battle Shared Pokebilities", RANDOM_BATTLE_MAYHEM,
            "pokebilities,!scalemonsmod,!camomonsmod,!inversemod", "sp", "pokebilities", ""),
        new("Random Doubles Battle 2v2", RANDOM_BATTLE, "maxteamsize=4,pickedteamsize=2,bestof=3", "1v1", "team preview", ""),
        new("Random Battle MonoSpectre Inverse", RANDOM_BATTLE, "forcemonotype=ghost,inversemod", "inverse", "", ""),
        new("Super Staff Bros Ultimate", "superstaffbrosultimate", "", "ssb", "", ""),
        new("Broken Cup Double Sharing", "brokencup", "sharingiscaring,mayhem,!scalemonsmod,!camomonsmod,!inversemod",
            "spnl", "sharingiscaring", ""),
        new("Mega Broken Cup Shared Power", "hackmonscup",
            "-allpokemon,-allabilities,+adaptability,+angershell,+beadsofruin,+download,+fluffy,+furcoat,+goodasgold,+hugepower,+icescales,+illusion,+innardsout,+magicbounce,+magicguard,+moldbreaker,+moody,+multiscale,+opportunist,+prankster,+purepower,+purifyingsalt,+regenerator,+sheerforce,+speedboost,+stakeout,+stamina,+parentalbond,+swordofruin,+tabletsofruin,+teravolt,+tintedlens,+toughclaws,+toxicchain,+toxicdebris,+triage,+unaware,+vesselofruin,+waterbubble,+analytic,+cursedbody,+effectspore,-allmoves,+Bitter Blade,+Drain Punch,+Giga Drain,+Heal Order,+Horn Leech,+Leech Life,+Matcha Gotcha,+Milk Drink,+Moonlight,+Morning Sun,+Oblivion Wing,+Parabolic Charge,+Recover,+Revival Blessing,+Roost,+Shore Up,+Slack Off,+Soft-Boiled,+Strength Sap,+Synthesis,+Wish,+vcreate,+sacredfire,+firelash,+flamecharge,+blueflare,+searingshot,+fierydance,+mysticalfire,+oceanicoperetta,+steameruption,+originpulse,+scald,+fishiousrend,+aquastep,+flipturn,+batonpass,+surgingstrikes,+flowertrick,+gravapple,+trailblaze,+seedflare,+appleacid,+boomburst,+technoblast,+revelationdance,+pulverizingpancake,+multiattack,+combattorque,+flyingpress,+thunderouskick,+triplearrows,+bodypress,+circlethrow,+focusblast,+secretsword,+lightofruin,+fleurcannon,+moonblast,+guardianofalola,+letssnuggleforever,+magicaltorque,+lightthatburnsthesky,+futuresight,+photongeyser,+luminacrash,+esperwing,+bugbuzz,+uturn,+splinteredstormshards,+diamondstorm,+stoneaxe,+saltcure,+glaciallance,+tripleaxel,+frostbreath,+freezedry,+doomdesire,+makeitrain,+searingsunrazesmash,+gigatonhammer,+anchorshot,+doubleironbash,+catastropika,+boltstrike,+plasmafists,+boltbeak,+10000000voltthunderbolt,+electrodrift,+voltswitch,+discharge,+earthpower,+precipiceblades,+thousandarrows,+thousandwaves,+noxioustorque,+direclaw,+mortalspin,+rapidspin,+malignantchain,+shellsidearm,+clearsmog,+aeroblast,+chatter,+skyattack,+dragonascent,+beakblast,+foulplay,+wickedblow,+ceaselessedge,+pursuit,+knockoff,+fierywrath,+maliciousmoonsault,+menacingmoonrazemaelstrom,+astralbarrage,+moongeistbeam,+clangoroussoulblaze,+coreenforcer,+dragontail,+dragondarts,+scaleshot,+accelerock,+aquajet,+extremespeed,+fakeout,+firstimpression,+iceshard,+jetpunch,+machpunch,+shadowsneak,+suckerpunch,+thunderclap,+watershuriken,+quiverdance,+stickyweb,+tailglow,+nastyplot,+partingshot,+taunt,+topsyturvy,+geomancy,+clangoroussoul,+victorydance,+burningbulwark,+defog,+destinybond,+baddybad,+bouncybubble,+buzzybuzz,+freezyfrost,+glitzyglow,+sappyseed,+sizzlyslide,+sparklyswirl,+chillyreception,+coil,+trickroom,+teleport,+stealthrock,+spikes,+kingsshield,+shiftgear,+acupressure,+assist,+encore,+extremeevoboost,+glare,+naturepower,+perishsong,+shellsmash,+swordsdance,+tidyup,+transform,+roar,+whirlwind,+yawn,+gmaxsteelsurge,-allitems,+choiceband,+choicescarf,+choicespecs,+heavydutyboots,+lifeorb,+rockyhelmet,+leftovers,+sitrusberry,+aguavberry,+weaknesspolicy,+redcard,+lumberry,+magoberry,+item:metronome,+brightpowder,+Abomasnow-Mega,+Absol-Mega,+Aerodactyl-Mega,+Aggron-Mega,+Alakazam-Mega,+Altaria-Mega,+Ampharos-Mega,+Audino-Mega,+Banette-Mega,+Beedrill-Mega,+Blastoise-Mega,+Blaziken-Mega,+Camerupt-Mega,+Charizard-Mega-X,+Charizard-Mega-Y,+Diancie-Mega,+Gallade-Mega,+Garchomp-Mega,+Gardevoir-Mega,+Gengar-Mega,+Glalie-Mega,+Gyarados-Mega,+Heracross-Mega,+Houndoom-Mega,+Kangaskhan-Mega,+Latias-Mega,+Latios-Mega,+Lopunny-Mega,+Lucario-Mega,+Manectric-Mega,+Mawile-Mega,+Medicham-Mega,+Metagross-Mega,+Mewtwo-Mega-X,+Mewtwo-Mega-Y,+Pidgeot-Mega,+Pinsir-Mega,+Rayquaza-Mega,+Sableye-Mega,+Salamence-Mega,+Sceptile-Mega,+Scizor-Mega,+Sharpedo-Mega,+Slowbro-Mega,+Steelix-Mega,+Swampert-Mega,+Tyranitar-Mega,+Venusaur-Mega,pokebilities",
            "spnl", "mmc6", "megabroken"),
        new("Pick Your Team Shared Power", RANDOM_BATTLE_MAYHEM,
            "!scalemonsmod,!camomonsmod,!inversemod,maxteamsize=18,pickedteamsize=6", "sp", "", ""),
        new("Battle Factory Foresighters Voltturn", "battlefactory", "foresighters,voltturnmayhemmod", "foresighters",
            "voltturn", ""),
        new("Battle Factory Bonus Type", "battlefactory", "bonustypemod", "bt", "", "")
    ];
    
    private readonly IRandomService _randomService;

    public RandomTournamentCommand(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public override Rank RequiredRank => Rank.Driver;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var tournament = _randomService.RandomElement(TOURNAMENTS);

        context.Reply($"/tour create {tournament.Tier}, elim");
        context.Reply($"/tour name {tournament.Name}");
        context.Reply($"/wall {context.GetString("random_tournament_wall", tournament.Name)}");

        if (!string.IsNullOrEmpty(tournament.Rules))
        {
            context.Reply($"/tour rules {tournament.Rules}");
        }

        if (!string.IsNullOrEmpty(tournament.Rfaq1))
        {
            context.Reply($"!rfaq {tournament.Rfaq1}");
        }

        if (!string.IsNullOrEmpty(tournament.Rfaq2))
        {
            context.Reply($"!rfaq {tournament.Rfaq2}");
        }

        if (!string.IsNullOrEmpty(tournament.Rfaq3))
        {
            context.Reply($"!rfaq {tournament.Rfaq3}");
        }

        return Task.CompletedTask;
    }
}