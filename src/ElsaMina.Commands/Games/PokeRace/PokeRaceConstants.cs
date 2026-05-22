namespace ElsaMina.Commands.Games.PokeRace;

public record PokemonRaceData(int Speed, string Image, string MiniSprite, string Type);

public record RaceEvent(string Name, string TextTemplate, double Effect, string EventType);

public static class PokeRaceConstants
{
    public static readonly TimeSpan AUTO_START_DELAY = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan UPDATE_INTERVAL = TimeSpan.FromSeconds(5);
    public const int RACE_LENGTH = 12;
    public const int MIN_PLAYERS = 2;
    public const int MAX_RECENT_EVENTS = 10;

    public static readonly IReadOnlyDictionary<string, PokemonRaceData> RACE_POKEMON =
        new Dictionary<string, PokemonRaceData>
        {
            ["Rapidash"] = new(105, "https://www.shinyhunters.com/images/regular/78.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/rapidash.gif", "Feu"),
            ["Electrode"] = new(150, "https://www.shinyhunters.com/images/regular/101.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/electrode.gif", "Électrik"),
            ["Jolteon"] = new(130, "https://www.shinyhunters.com/images/regular/135.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/jolteon.gif", "Électrik"),
            ["Aerodactyl"] = new(130, "https://www.shinyhunters.com/images/regular/142.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/aerodactyl.gif", "Roche/Vol"),
            ["Crobat"] = new(130, "https://www.shinyhunters.com/images/regular/169.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/crobat.gif", "Poison/Vol"),
            ["Deoxys"] = new(150, "https://www.shinyhunters.com/images/regular/386.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/deoxys-speed.gif", "Psy"),
            ["Ninjask"] = new(160, "https://www.shinyhunters.com/images/regular/291.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/ninjask.gif", "Insecte/Vol"),
            ["Accelgor"] = new(145, "https://www.shinyhunters.com/images/regular/617.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/accelgor.gif", "Insecte"),
            ["Darkrai"] = new(160, "https://www.shinyhunters.com/images/regular/491.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/darkrai.gif", "Ténèbre"),
            ["Zeraora"] = new(143, "https://www.shinyhunters.com/images/regular/807.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/zeraora.gif", "Électrik"),
            ["Dragapult"] = new(142, "https://www.shinyhunters.com/images/regular/887.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/dragapult.gif", "Dragon/Spectre"),
            ["Frosmoth"] = new(183, "https://www.shinyhunters.com/images/regular/873.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/frosmoth.gif", "Glace/Insecte"),
            ["Sceptile"] = new(120, "https://www.shinyhunters.com/images/regular/254.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/sceptile.gif", "Plante"),
            ["Floatzel"] = new(115, "https://www.shinyhunters.com/images/regular/419.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/floatzel.gif", "Eau"),
            ["Weavile"] = new(125, "https://www.shinyhunters.com/images/regular/461.gif",
                "https://play.pokemonshowdown.com/sprites/gen5ani/weavile.gif", "Glace/Ténèbre"),
        };

    private const string BOOST = "boost";
    private const string NEUTRAL = "neutral";
    private const string SLOW = "slow";

    public static readonly IReadOnlyList<RaceEvent> RACE_EVENTS = new List<RaceEvent>
    {
        // Boosts
        new("boost", "{pokemon} a trouvé un raccourci et gagne une avance!", 2, BOOST),
        new("boost_big", "{pokemon} utilise une attaque spéciale et fait un bond en avant!", 3, BOOST),
        new("nitro", "{pokemon} active son turbo et dépasse ses adversaires!", 4, BOOST),
        new("tailwind", "{pokemon} bénéficie d'un vent favorable!", 2.5, BOOST),
        new("item", "{pokemon} utilise une Vive Griffe pour accélérer!", 2, BOOST),
        // Neutral
        new("normal", "{pokemon} maintient son allure!", 0, NEUTRAL),
        new("focus", "{pokemon} se concentre sur la course!", 0.5, NEUTRAL),
        new("cheer", "{pokemon} est encouragé par la foule!", 1, NEUTRAL),
        // Slows
        new("slow", "{pokemon} a trébuché et perd du terrain!", -1, SLOW),
        new("slow_big", "{pokemon} est distrait par un obstacle et perd beaucoup de terrain!", -2, SLOW),
        new("mud", "{pokemon} s'est enlisé dans de la boue!", -1.5, SLOW),
        new("tired", "{pokemon} commence à fatiguer!", -1, SLOW),
        // Special
        new("leader_slow", "{pokemon} est trop confiant et ralentit!", -3, "leader_penalty"),
        new("comeback", "{pokemon} refuse d'abandonner et accélère!", 5, "trailing_boost"),
    };
}