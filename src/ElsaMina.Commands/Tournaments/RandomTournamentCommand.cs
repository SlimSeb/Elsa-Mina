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
        new("Random Battle Monotype", "monotyperandombattle", "", "mono", "", ""),
        new("Random Super Metronome", "superstaffbrosultimate", "dynamaxclause", "super metronome", "", ""),
        new("Battle Factory Shared Power", "battlefactory", "mayhem,!scalemonsmod,!camomonsmod,!inversemod", "spnl", "",
            ""),
        new("Random Battle 1v1 Bo3", RANDOM_BATTLE,
            "maxteamsize=3,pickedteamsize=1,bestof=3,teampreview,terastalclause", "1v1", "", ""),
        new("[Gen 8] Random CAP 6v6", "gen8cap1v1", "!!maxteamsize=6,!!pickedteamsize=6", "cap", "", ""),
        new("Broken Cup Shared Power", "brokencup", "mayhem,!scalemonsmod,!camomonsmod,!inversemod", "spnl", "", ""),
        new("[Gen 7] Random Protean Shared", "gen7randombattle",
            "proteanpalacemod,mayhem,!scalemonsmod,!camomonsmod,!inversemod", "spnl", "protean", ""),
        new("Random Bonus Type Revelationmons", RANDOM_BATTLE, "bonustypemod,revelationmonsmod", "revelationmons", "bt",
            ""),
        new("Random Battle Camomons", RANDOM_BATTLE, "camomonsmod", "camo", "", ""),
        new("Random Battle Inverse", RANDOM_BATTLE, "inversemod", "inverse", "", ""),
        new("Baby Random Battle", "babyrandombattle", "", "babyrandombattle", "", ""),
        new("Random Battle Protean Palace", RANDOM_BATTLE, "proteanpalacemod", "proteanpalace", "", ""),
        new("Random Battle First Blood Bo3", RANDOM_BATTLE, "firstbloodrule,bestof=3", "firstblood", "", ""),
        new("Random Battle Mort Subite", RANDOM_BATTLE_MAYHEM,
            "firstbloodrule,maxteamsize=24,inversemod,!scalemonsmod,!camomonsmod,proteanpalacemod", "firstblood", "sp",
            "proteanpalace"),
        new("Random Battle Trop d'Attaques", RANDOM_BATTLE, "maxmovecount=6,forceofthefallenmod", "mmc6", "fotf", ""),
        new("Random Battle Shared Pokebilities", RANDOM_BATTLE_MAYHEM,
            "pokebilities,!scalemonsmod,!camomonsmod,!inversemod", "sp", "pokebilities", ""),
        new("Random Doubles Battle 2v2", RANDOM_BATTLE, "maxteamsize=4,pickedteamsize=2,bestof=3,teampreview", "1v1",
            "", ""),
        new("Random Battle MonoSpectre Inverse", RANDOM_BATTLE, "forcemonotype=ghost,inversemod", "inverse", "", ""),
        new("Super Staff Bros Ultimate", "superstaffbrosultimate", "", "ssb", "", ""),
        new("Broken Cup Double Sharing", "brokencup", "sharingiscaring,mayhem,!scalemonsmod,!camomonsmod,!inversemod",
            "spnl", "sharingiscaring", ""),
        new("Mega Broken Cup Shared Power", "hackmonscup",
            "-allpokemon,-allabilities,+adaptability,+angershell,+beadsofruin,+download,+fluffy,+furcoat,+goodasgold,+hugepower,+icescales,+illusion,+innardsout,+magicbounce,+magicguard,+moldbreaker,+moody,+multiscale,+opportunist,+prankster,+purepower,+purifyingsalt,+regenerator,+sheerforce,+speedboost,+stakeout,+stamina,+parentalbond,+swordofruin,+tabletsofruin,+teravolt,+tintedlens,+toughclaws,+toxicchain,+toxicdebris,+triage,+unaware,+vesselofruin,+waterbubble,+analytic,+cursedbody,+effectspore,-allmoves,+Bitter Blade,+Drain Punch,+Giga Drain,+Heal Order,+Horn Leech,+Leech Life,+Matcha Gotcha,+Milk Drink,+Moonlight,+Morning Sun,+Oblivion Wing,+Parabolic Charge,+Recover,+Revival Blessing,+Roost,+Shore Up,+Slack Off,+Soft-Boiled,+Strength Sap,+Synthesis,+Wish,+vcreate,+sacredfire,+firelash,+flamecharge,+blueflare,+searingshot,+fierydance,+mysticalfire,+oceanicoperetta,+steameruption,+originpulse,+scald,+fishiousrend,+aquastep,+flipturn,+batonpass,+surgingstrikes,+flowertrick,+gravapple,+trailblaze,+seedflare,+appleacid,+boomburst,+technoblast,+revelationdance,+pulverizingpancake,+multiattack,+combattorque,+flyingpress,+thunderouskick,+triplearrows,+bodypress,+circlethrow,+focusblast,+secretsword,+lightofruin,+fleurcannon,+moonblast,+guardianofalola,+letssnuggleforever,+magicaltorque,+lightthatburnsthesky,+futuresight,+photongeyser,+luminacrash,+esperwing,+bugbuzz,+uturn,+splinteredstormshards,+diamondstorm,+stoneaxe,+saltcure,+glaciallance,+tripleaxel,+frostbreath,+freezedry,+doomdesire,+makeitrain,+searingsunrazesmash,+gigatonhammer,+anchorshot,+doubleironbash,+catastropika,+boltstrike,+plasmafists,+boltbeak,+10000000voltthunderbolt,+electrodrift,+voltswitch,+discharge,+earthpower,+precipiceblades,+thousandarrows,+thousandwaves,+noxioustorque,+direclaw,+mortalspin,+rapidspin,+malignantchain,+shellsidearm,+clearsmog,+aeroblast,+chatter,+skyattack,+dragonascent,+beakblast,+foulplay,+wickedblow,+ceaselessedge,+pursuit,+knockoff,+fierywrath,+maliciousmoonsault,+menacingmoonrazemaelstrom,+astralbarrage,+moongeistbeam,+clangoroussoulblaze,+coreenforcer,+dragontail,+dragondarts,+scaleshot,+accelerock,+aquajet,+extremespeed,+fakeout,+firstimpression,+iceshard,+jetpunch,+machpunch,+shadowsneak,+suckerpunch,+thunderclap,+watershuriken,+quiverdance,+stickyweb,+tailglow,+nastyplot,+partingshot,+taunt,+topsyturvy,+geomancy,+clangoroussoul,+victorydance,+burningbulwark,+defog,+destinybond,+baddybad,+bouncybubble,+buzzybuzz,+freezyfrost,+glitzyglow,+sappyseed,+sizzlyslide,+sparklyswirl,+chillyreception,+coil,+trickroom,+teleport,+stealthrock,+spikes,+kingsshield,+shiftgear,+acupressure,+assist,+encore,+extremeevoboost,+glare,+naturepower,+perishsong,+shellsmash,+swordsdance,+tidyup,+transform,+roar,+whirlwind,+yawn,+gmaxsteelsurge,-allitems,+choiceband,+choicescarf,+choicespecs,+heavydutyboots,+lifeorb,+rockyhelmet,+leftovers,+sitrusberry,+aguavberry,+weaknesspolicy,+redcard,+lumberry,+magoberry,+item:metronome,+brightpowder,+Abomasnow-Mega,+Absol-Mega,+Aerodactyl-Mega,+Aggron-Mega,+Alakazam-Mega,+Altaria-Mega,+Ampharos-Mega,+Audino-Mega,+Banette-Mega,+Beedrill-Mega,+Blastoise-Mega,+Blaziken-Mega,+Camerupt-Mega,+Charizard-Mega-X,+Charizard-Mega-Y,+Diancie-Mega,+Gallade-Mega,+Garchomp-Mega,+Gardevoir-Mega,+Gengar-Mega,+Glalie-Mega,+Gyarados-Mega,+Heracross-Mega,+Houndoom-Mega,+Kangaskhan-Mega,+Latias-Mega,+Latios-Mega,+Lopunny-Mega,+Lucario-Mega,+Manectric-Mega,+Mawile-Mega,+Medicham-Mega,+Metagross-Mega,+Mewtwo-Mega-X,+Mewtwo-Mega-Y,+Pidgeot-Mega,+Pinsir-Mega,+Rayquaza-Mega,+Sableye-Mega,+Salamence-Mega,+Sceptile-Mega,+Scizor-Mega,+Sharpedo-Mega,+Slowbro-Mega,+Steelix-Mega,+Swampert-Mega,+Tyranitar-Mega,+Venusaur-Mega,pokebilities",
            "spnl", "mmc6", "megabroken"),
        new("Random Battle Shared Power B18P6", RANDOM_BATTLE_MAYHEM,
            "!scalemonsmod,!camomonsmod,!inversemod,maxteamsize=18,pickedteamsize=6", "sp", "", ""),
        new("Battle Factory Foresighters Voltturn B8P4", "battlefactory",
            "foresighters,voltturnmayhemmod,maxteamsize=8,pickedteamsize=4", "foresighters", "voltturn", ""),
        new("Battle Factory Bonus Type", "battlefactory", "bonustypemod", "bt", "", ""),
        new("[SV] BSS Shared Power", "bssfactory", "mayhem,!scalemonsmod,!camomonsmod,!inversemod", "spnl", "", ""),
        new("[Champions] Random Shared Power B12P6", "championsrandombattle", "randombattlesharedpowerb12p6", "spnl",
            "", ""),
        new("Baby Random Shared Power B18P6", "babyrandombattle", "randombattlesharedpowerb12p6,!!maxteamsize=18",
            "spnl", "", ""),
        new("Monotype Random Shared B12P6", "randombattlesharedpowerb12p6", "sametypeclause", "mono", "", ""),
        new("1v1 Factory Bo3", "1v1factory", "bestof=3", "1v1", "", ""),
        new("[Let's Go] Random Battle Protean Palace", "letsgorandombattle", "proteanpalacemod", "protean", "", ""),
        new("[Gen 1] Random Battle FOTF", "gen1randombattle", "forceofthefallenmod", "fotf", "", ""),
        new("[Gen 1~9] Random Roulette FOTF", "randomroulette", "forceofthefallenmod", "fotf", "", ""),
        new("[Gen 8] Random Battle Sans Dynamax", "gen8randombattle", "dynamaxclause", "", "", ""),
        new("[BDSP] Random Battle", "gen8bdsprandombattle", "", "", "", ""),
        new("Random Battle MonoGlace", "gen9randombattle", "forcemonotype=ice", "", "", ""),
        new("Challenge Cup VGC 12 attaques", "challengecup2v2", "!!pickedteamsize=4,maxmovecount=12", "", "", ""),
        new("Challenge Cup 1v1 Mono Normal", "challengecup1v1", "forcemonotype=normal", "", "", ""),
        new("Hackmons Cup MonoDragon B11P6", "hackmonscup",
            "-allpokemon,+Altaria,+Appletun,+Applin,+Arceus-Dragon,+Archaludon,+Arctibax,+Axew,+Bagon,+Baxcalibur,+Cyclizar,+Deino,+Dialga,+Dipplin,+Dracovish,+Dracozolt,+Dragalge,+Dragapult,+Dragonair,+Dragonite,+Drakloak,+Drampa,+Dratini,+Dreepy,+Druddigon,+Duraludon,+Eternatus,+Exeggutor-Alola,+Flapple,+Flygon,+Fraxure,+Frigibax,+Gabite,+Garchomp,+Gible,+Giratina,+Goodra,+Goodra-Hisui,+Goomy,+Gouging Fire,+Guzzlord,+Hakamo-o,+Haxorus,+Hydrapple,+Hydreigon,+Jangmo-o,+Kingdra,+Kommo-o,+Koraidon,+Kyurem,+Latias,+Latios,+Miraidon,+Naganadel,+Necrozma-Ultra,+Noibat,+Noivern,+Palkia,+Raging Bolt,+Rayquaza,+Regidrago,+Reshiram,+Roaring Moon,+Salamence,+Shelgon,+Silvally-Dragon,+Sliggoo,+Sliggoo-Hisui,+Tatsugiri,+Turtonator,+Tyrantrum,+Tyrunt,+Vibrava,+Walking Wake,+Zekrom,+Zweilous,+Zygarde,+Astrolotl,+Chuggalong,+Chuggon,+Cyclohm,+Draggalong,+Duohm,+Miasmaw,+Miasmite,+Pajantom,+Solotl,+charizardmegax,+dragonitemega,+feraligatrmega,+ampharosmega,+sceptilemega,+salamencemega,+latiosmega,+latiasmega,+rayquazamega,+dialgaorigin,+palkiaorigin,+giratinaorigin,+kyuremwhite,+kyuremblack,+dragalgemega,+zygarde10,+zygardecomplete,+zygardemega,+drampamega,+flapplegmax,+appletungmax,+duraludongmax,+eternatuseternamax,+tatsugiridroopy,+tatsugiridroopymega,+tatsugiristretchymega,+tatsugiristretchy,+tatsugiricurlymega,+baxcaliburmega,-allmoves,+Breaking Swipe,+Clanging Scales,+Clangorous Soul,+Clangorous Soulblaze,+Core Enforcer,+Draco Meteor,+Dragon Breath,+Dragon Cheer,+Dragon Claw,+Dragon Dance,+Dragon Darts,+Dragon Energy,+Dragon Hammer,+Dragon Pulse,+Dragon Rage,+Dragon Rush,+Dragon Tail,+Dual Chop,+Dynamax Cannon,+Eternabeam,+Fickle Beam,+Glaive Rush,+Hidden Power Dragon,+Order Up,+Outrage,+Roar of Time,+Scale Shot,+Spacial Rend,+Twister,-allabilities,+toughclaws,+frisk,+harvest,+shedskin,+marvelscale,+multiscale,+moldbreaker,+sniper,+naturalcure,+sheerforce,+intimidate,+moxie,+roughskin,+pressure,+rivalry,+unnerve,+hustle,+turboblaze,+teravolt,+poisontouch,+poisonpoint,+adaptability,+sturdy,+gooey,+shellarmor,+berserk,+bulletproof,+soundproof,+beastboost,+neuroforce,+ripen,+gluttony,+clearbody,+cursedbody,+dragonsmaw,+regenerator,+protosynthesis,+orichalcumpulse,+hadronengine,+static,+shielddust,+comatose,+magician,+compoundeyes,+whitesmoke,+slowstart,maxteamsize=11,pickedteamsize=6,-allitems,+dracoplate,+dragonfang,+dragongem,+dragoniumz,+habanberry,+tr47,+tr51,+tr62,+sitrusberry,+leftovers,+lifeorb,+choicespecs,+choicescarf,+choiceband,+assaultvest,+aguavberry,+expertbelt,+figyberry,+magoberry,+rockyhelmet,+salacberry,+wikiberry,+adrenalineorb,+kingsrock,+razorfang,+scopelens,forceofthefallenmod,forceteratype=dragon,brokenrecordmod,forceopenteamsheets,pokebilities",
            "fotf", "pokebilities", ""),
        new("Hackmons Cup Mono Sommeil", "hackmonscup",
            "-allabilities,+comatose,pokemon,+darkrai,+cresselia,+musharna,+hypno,+snorlax,+komala,+igglypuff,+poliwrath,+butterfree,+exeggutor,+lunala,+pajantom,+pokestarmonster,+uxie,+amoonguss,maxmovecount=3,+snore,+sleeptalk,+dreameater,+nightmare,+roar,+dragontail,+stealthrock,+defog,+fakeout,+extremespeed,+rapidspin,+gmaxsteelsurge,+spikes,+wakeupslap,+hex,+assist,+mirrormove,+futuresight,forceofthefallenmod,pokebilities,!teampreview,-allitems,+sitrusberry,+ejectbutton,+choicescarf,+leftovers,+heavydutyboots,+redcard,datapreview,+gengarmega,mayhem,!scalemonsmod,!inversemod,!camomonsmod,!teampreview,maxmovecount=3,+snore,+sleeptalk,+dreameater,+nightmare,+roar,+dragontail,+stealthrock,+defog,+fakeout,+extremespeed,+rapidspin,+gmaxsteelsurge,+spikes,+wakeupslap,+hex,+assist,+mirrormove,+futuresight,forceofthefallenmod,pokebilities,!teampreview,-allitems,+sitrusberry,+ejectbutton,+choicescarf,+leftovers,+heavydutyboots,+redcard,datapreview",
            "fotf", "pokebilities", ""),
        new("Troubadour du Fun BSS Factory", "bssfactory", "mayhem,!inversemod,!camomonsmod,voltturnmayhemmod", "scale",
            "spnl", "vtm"),
        new("[Gen 7] Random Battle Level 1", "gen7randombattle", "adjustlevel=1", "", "", ""),
        new("[Gen 5] Random Type Split BnB", "gen5randombattle", "typesplit,badnboostedmod,datapreview", "bnb",
            "typesplit", ""),
        new("[Gen 6] Random Battle Revelation", "gen6randombattle", "revelationmonsmod", "", "", ""),
        new("[Gen 5] Random Battle Tier Shift", "gen5randombattle", "tiershiftmod,datapreview,adjustlevel=100", "", "",
            ""),
        new("[Gen 3] Random Battle B12P6", "gen3randombattle", "maxteamsize=12,pickedteamsize=6,teampreview",
            "maxteamsize", "", ""),
        new("[Gen 4] Random Sharing is Caring", "gen4randombattle", "sharingiscaring", "sharingiscaring", "", ""),
        new("Battle Factory 1 Move FOTF B12P6", "battlefactory",
            "forceofthefallenmod,maxteamsize=12,pickedteamsize=6,maxmovecount=1", "fotf", "", ""),
        new("[Gen 5] Random Shared 2-3 moves FOTF B12P6", "gen5randombattle",
            "randombattlesharedpowerb12p6,maxmovecount=1,forceofthefallenmod", "fotf", "spnl", ""),
        new("[Gen 7] Random Battle MonoNormal", "gen7randombattle", "forcemonotype=normal", "", "", ""),
        new("[Gen 6] Random Battle Protean Palace", "gen6randombattle", "proteanpalacemod", "protean", "", ""),
        new("Hackmons Cup Mono Pikachu", "doubleshackmonscup",
            "-allpokemon,+pikachu,+pikachurockstar,+pikachubelle,+pikachupopstar,+pikachuphd,+pikachulibre,+pikachuoriginal,+pikachuhoenn,+pikachusinnoh,+pikachuunova,+pikachukalos,+pikachualola,+pikachupartner,+pikachustarter,+pikachuworld,+raichu,+raichualola,+raichumegax,+raichumegay,+pichu,+pichuspikyeared,+plusle,+minun,+emolga,+dedenne,+togedemaru,+mimikyu,+morpeko,+morpekohangry,+pawmot,+pikachucosplay,-allitems,+lightball,+electricgem,+cellbattery,+electriumz,+magnet,+zapplate,-allabilities,+static,+electricsurge,+noguard,+lightningrod,+surgesurfer,+galvanize,+plus,+minus,+motordrive,+pickup,+sturdy,+ironbarbs,+colorchange,+voltabsorb,+transistor,+electromorphosis,+quarkdrive,-allmoves,+alluringvoice,+discharge,+encore,+extremespeed,+fakeout,+irontail,+knockoff,+nuzzle,+playrough,+quickattack,+protect,+detect,+surf,+terablast,+thunder,+thunderbolt,+thunderpunch,+thunderwave,+trailblaze,+voltswitch,+volttackle,+wildcharge,+chargebeam,+charm,+risingvoltage,+zapcannon,+focusblast,+shadowsneak,+acrobatics,+helpinghand,+zingzap,+zippyzap,+floatyfall,+splishysplash,+pikapapow,+clefairy,+eeveestarter,+buzzybuzz,+thundershock,+meteormash,+iciclecrash,+drainingkiss,+electricterrain,+flyingpress,+revivalblessing,+doubleshock,+electrify,+paraboliccharge",
            "", "", "")
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