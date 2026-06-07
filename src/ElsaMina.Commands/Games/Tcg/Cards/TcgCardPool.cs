using System.Collections.ObjectModel;

namespace ElsaMina.Commands.Games.Tcg.Cards;

/// <summary>
/// The curated, hand-authored pool of cards available to deck builders. All entries are Basic
/// Pokémon. Each card has a stable collector-number <see cref="TcgCard.Id"/> independent of its
/// <see cref="TcgCard.Species"/> (the Showdown sprite slug that resolves <see cref="TcgCard.SpriteUrl"/>).
/// </summary>
public static class TcgCardPool
{
    private static readonly IReadOnlyDictionary<string, TcgCard> CardsById;

    public static IReadOnlyList<TcgCard> AllCards { get; }

    static TcgCardPool()
    {
        AllCards = BuildCards();
        CardsById = AllCards.ToDictionary(card => card.Id, StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryGet(string id, out TcgCard card)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            card = null;
            return false;
        }

        return CardsById.TryGetValue(id.Trim(), out card);
    }

    public static IEnumerable<TcgCard> ByType(TcgType type) => AllCards.Where(card => card.Type == type);

    private static TcgAttack Attack(string name, int damage, params TcgType[] cost) => new(name, cost, damage);

    private static ReadOnlyCollection<TcgCard> BuildCards() => new(new List<TcgCard>
    {
        // --- Grass ---
        new() { Id = "001", Species = "bulbasaur", Name = "Bulbasaur", Hp = 70, Type = TcgType.Grass, Weakness = TcgType.Fire, RetreatCost = 1,
            Attacks = [Attack("Vine Whip", 20, TcgType.Grass, TcgType.Colorless)] },
        new() { Id = "002", Species = "oddish", Name = "Oddish", Hp = 60, Type = TcgType.Grass, Weakness = TcgType.Fire, RetreatCost = 1,
            Attacks = [Attack("Sprout", 10, TcgType.Grass)] },
        new() { Id = "003", Species = "tangela", Name = "Tangela", Hp = 80, Type = TcgType.Grass, Weakness = TcgType.Fire, RetreatCost = 2,
            Attacks = [Attack("Absorb", 20, TcgType.Grass), Attack("Slam", 40, TcgType.Grass, TcgType.Colorless)] },
        new() { Id = "004", Species = "scyther", Name = "Scyther", Hp = 90, Type = TcgType.Grass, Weakness = TcgType.Fire, RetreatCost = 1,
            Attacks = [Attack("Slash", 30, TcgType.Grass, TcgType.Colorless), Attack("Cross Cut", 60, TcgType.Grass, TcgType.Grass, TcgType.Colorless)] },
        new() { Id = "005", Species = "venusaur", Name = "Venusaur ex", Hp = 160, Type = TcgType.Grass, Weakness = TcgType.Fire, RetreatCost = 3, IsEx = true,
            Attacks = [Attack("Razor Leaf", 60, TcgType.Grass, TcgType.Colorless, TcgType.Colorless), Attack("Giant Bloom", 100, TcgType.Grass, TcgType.Grass, TcgType.Colorless, TcgType.Colorless)] },

        // --- Fire ---
        new() { Id = "006", Species = "charmander", Name = "Charmander", Hp = 60, Type = TcgType.Fire, Weakness = TcgType.Water, RetreatCost = 1,
            Attacks = [Attack("Ember", 30, TcgType.Fire, TcgType.Colorless)] },
        new() { Id = "007", Species = "vulpix", Name = "Vulpix", Hp = 60, Type = TcgType.Fire, Weakness = TcgType.Water, RetreatCost = 1,
            Attacks = [Attack("Confuse Ray", 20, TcgType.Fire)] },
        new() { Id = "008", Species = "growlithe", Name = "Growlithe", Hp = 70, Type = TcgType.Fire, Weakness = TcgType.Water, RetreatCost = 1,
            Attacks = [Attack("Bite", 30, TcgType.Fire, TcgType.Colorless)] },
        new() { Id = "009", Species = "magmar", Name = "Magmar", Hp = 90, Type = TcgType.Fire, Weakness = TcgType.Water, RetreatCost = 2,
            Attacks = [Attack("Fire Punch", 40, TcgType.Fire, TcgType.Fire)] },
        new() { Id = "010", Species = "charizard", Name = "Charizard ex", Hp = 180, Type = TcgType.Fire, Weakness = TcgType.Water, RetreatCost = 3, IsEx = true,
            Attacks = [Attack("Slash", 60, TcgType.Fire, TcgType.Colorless, TcgType.Colorless), Attack("Crimson Storm", 130, TcgType.Fire, TcgType.Fire, TcgType.Colorless, TcgType.Colorless)] },

        // --- Water ---
        new() { Id = "011", Species = "squirtle", Name = "Squirtle", Hp = 70, Type = TcgType.Water, Weakness = TcgType.Lightning, RetreatCost = 1,
            Attacks = [Attack("Water Gun", 20, TcgType.Water, TcgType.Colorless)] },
        new() { Id = "012", Species = "psyduck", Name = "Psyduck", Hp = 60, Type = TcgType.Water, Weakness = TcgType.Lightning, RetreatCost = 1,
            Attacks = [Attack("Headache", 10, TcgType.Water)] },
        new() { Id = "013", Species = "staryu", Name = "Staryu", Hp = 60, Type = TcgType.Water, Weakness = TcgType.Lightning, RetreatCost = 1,
            Attacks = [Attack("Swift", 20, TcgType.Colorless, TcgType.Colorless)] },
        new() { Id = "014", Species = "seel", Name = "Seel", Hp = 70, Type = TcgType.Water, Weakness = TcgType.Lightning, RetreatCost = 1,
            Attacks = [Attack("Headbutt", 20, TcgType.Water, TcgType.Colorless)] },
        new() { Id = "015", Species = "lapras", Name = "Lapras", Hp = 100, Type = TcgType.Water, Weakness = TcgType.Lightning, RetreatCost = 2,
            Attacks = [Attack("Water Gun", 30, TcgType.Water, TcgType.Colorless), Attack("Hydro Pump", 70, TcgType.Water, TcgType.Water, TcgType.Colorless)] },
        new() { Id = "016", Species = "blastoise", Name = "Blastoise ex", Hp = 170, Type = TcgType.Water, Weakness = TcgType.Lightning, RetreatCost = 3, IsEx = true,
            Attacks = [Attack("Surf", 60, TcgType.Water, TcgType.Colorless, TcgType.Colorless), Attack("Hydro Bazooka", 100, TcgType.Water, TcgType.Water, TcgType.Colorless, TcgType.Colorless)] },

        // --- Lightning ---
        new() { Id = "017", Species = "pikachu", Name = "Pikachu", Hp = 60, Type = TcgType.Lightning, Weakness = TcgType.Fighting, RetreatCost = 1,
            Attacks = [Attack("Thunder Jolt", 30, TcgType.Lightning, TcgType.Colorless)] },
        new() { Id = "018", Species = "magnemite", Name = "Magnemite", Hp = 60, Type = TcgType.Lightning, Weakness = TcgType.Fighting, RetreatCost = 1,
            Attacks = [Attack("Thunder Wave", 20, TcgType.Lightning)] },
        new() { Id = "019", Species = "voltorb", Name = "Voltorb", Hp = 60, Type = TcgType.Lightning, Weakness = TcgType.Fighting, RetreatCost = 1,
            Attacks = [Attack("Tackle", 10, TcgType.Colorless)] },
        new() { Id = "020", Species = "electabuzz", Name = "Electabuzz", Hp = 90, Type = TcgType.Lightning, Weakness = TcgType.Fighting, RetreatCost = 2,
            Attacks = [Attack("Thunder Punch", 40, TcgType.Lightning, TcgType.Colorless)] },
        new() { Id = "021", Species = "zapdos", Name = "Zapdos ex", Hp = 140, Type = TcgType.Lightning, Weakness = TcgType.Fighting, RetreatCost = 2, IsEx = true,
            Attacks = [Attack("Peck", 40, TcgType.Colorless, TcgType.Colorless), Attack("Thundering Hurricane", 110, TcgType.Lightning, TcgType.Lightning, TcgType.Lightning)] },

        // --- Psychic ---
        new() { Id = "022", Species = "abra", Name = "Abra", Hp = 50, Type = TcgType.Psychic, Weakness = TcgType.Psychic, RetreatCost = 1,
            Attacks = [Attack("Psyshot", 20, TcgType.Psychic)] },
        new() { Id = "023", Species = "gastly", Name = "Gastly", Hp = 50, Type = TcgType.Psychic, Weakness = TcgType.Psychic, RetreatCost = 0,
            Attacks = [Attack("Lick", 10, TcgType.Psychic)] },
        new() { Id = "024", Species = "drowzee", Name = "Drowzee", Hp = 70, Type = TcgType.Psychic, Weakness = TcgType.Psychic, RetreatCost = 1,
            Attacks = [Attack("Pound", 20, TcgType.Colorless, TcgType.Colorless)] },
        new() { Id = "025", Species = "jynx", Name = "Jynx", Hp = 80, Type = TcgType.Psychic, Weakness = TcgType.Psychic, RetreatCost = 1,
            Attacks = [Attack("Psychic", 40, TcgType.Psychic, TcgType.Colorless)] },
        new() { Id = "026", Species = "mewtwo", Name = "Mewtwo ex", Hp = 150, Type = TcgType.Psychic, Weakness = TcgType.Psychic, RetreatCost = 2, IsEx = true,
            Attacks = [Attack("Psychic Sphere", 50, TcgType.Psychic, TcgType.Colorless), Attack("Psydrive", 120, TcgType.Psychic, TcgType.Psychic, TcgType.Colorless, TcgType.Colorless)] },

        // --- Fighting ---
        new() { Id = "027", Species = "machop", Name = "Machop", Hp = 70, Type = TcgType.Fighting, Weakness = TcgType.Psychic, RetreatCost = 1,
            Attacks = [Attack("Low Kick", 20, TcgType.Fighting)] },
        new() { Id = "028", Species = "geodude", Name = "Geodude", Hp = 80, Type = TcgType.Fighting, Weakness = TcgType.Grass, RetreatCost = 1,
            Attacks = [Attack("Tackle", 20, TcgType.Fighting, TcgType.Colorless)] },
        new() { Id = "029", Species = "mankey", Name = "Mankey", Hp = 60, Type = TcgType.Fighting, Weakness = TcgType.Psychic, RetreatCost = 1,
            Attacks = [Attack("Scratch", 20, TcgType.Colorless)] },
        new() { Id = "030", Species = "onix", Name = "Onix", Hp = 110, Type = TcgType.Fighting, Weakness = TcgType.Grass, RetreatCost = 3,
            Attacks = [Attack("Rock Throw", 30, TcgType.Fighting), Attack("Land Crush", 60, TcgType.Fighting, TcgType.Fighting, TcgType.Colorless)] },
        new() { Id = "031", Species = "hitmonlee", Name = "Hitmonlee", Hp = 90, Type = TcgType.Fighting, Weakness = TcgType.Psychic, RetreatCost = 2,
            Attacks = [Attack("High Jump Kick", 50, TcgType.Fighting, TcgType.Colorless)] },

        // --- Colorless ---
        new() { Id = "032", Species = "pidgey", Name = "Pidgey", Hp = 60, Type = TcgType.Colorless, Weakness = TcgType.Lightning, RetreatCost = 1,
            Attacks = [Attack("Gust", 20, TcgType.Colorless)] },
        new() { Id = "033", Species = "meowth", Name = "Meowth", Hp = 60, Type = TcgType.Colorless, Weakness = TcgType.Fighting, RetreatCost = 1,
            Attacks = [Attack("Pay Day", 10, TcgType.Colorless)] },
        new() { Id = "034", Species = "eevee", Name = "Eevee", Hp = 70, Type = TcgType.Colorless, Weakness = TcgType.Fighting, RetreatCost = 1,
            Attacks = [Attack("Tackle", 20, TcgType.Colorless, TcgType.Colorless)] },
        new() { Id = "035", Species = "jigglypuff", Name = "Jigglypuff", Hp = 70, Type = TcgType.Colorless, Weakness = TcgType.Fighting, RetreatCost = 1,
            Attacks = [Attack("Sing", 0, TcgType.Colorless), Attack("Pound", 20, TcgType.Colorless, TcgType.Colorless)] },
        new() { Id = "036", Species = "snorlax", Name = "Snorlax", Hp = 120, Type = TcgType.Colorless, Weakness = TcgType.Fighting, RetreatCost = 3,
            Attacks = [Attack("Body Slam", 50, TcgType.Colorless, TcgType.Colorless, TcgType.Colorless)] },
        new() { Id = "037", Species = "dratini", Name = "Dratini", Hp = 70, Type = TcgType.Colorless, Weakness = TcgType.Colorless, RetreatCost = 1,
            Attacks = [Attack("Wrap", 20, TcgType.Colorless, TcgType.Colorless)] },
        new() { Id = "038", Species = "tauros", Name = "Tauros", Hp = 100, Type = TcgType.Colorless, Weakness = TcgType.Fighting, RetreatCost = 1,
            Attacks = [Attack("Rampage", 30, TcgType.Colorless, TcgType.Colorless), Attack("Horn Drill", 70, TcgType.Colorless, TcgType.Colorless, TcgType.Colorless)] }
    });
}
