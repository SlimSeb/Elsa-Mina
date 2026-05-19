namespace ElsaMina.Battles.Data;

public static class TypeMatchupTable
{
    private static readonly Dictionary<PokemonType, Dictionary<PokemonType, double>> OffensiveMatchups =
        new()
        {
            [PokemonType.Normal] = new()
            {
                [PokemonType.Rock] = 0.5,
                [PokemonType.Ghost] = 0.0,
                [PokemonType.Steel] = 0.5
            },
            [PokemonType.Fire] = new()
            {
                [PokemonType.Fire] = 0.5,
                [PokemonType.Water] = 0.5,
                [PokemonType.Grass] = 2.0,
                [PokemonType.Ice] = 2.0,
                [PokemonType.Bug] = 2.0,
                [PokemonType.Rock] = 0.5,
                [PokemonType.Dragon] = 0.5,
                [PokemonType.Steel] = 2.0
            },
            [PokemonType.Water] = new()
            {
                [PokemonType.Fire] = 2.0,
                [PokemonType.Water] = 0.5,
                [PokemonType.Grass] = 0.5,
                [PokemonType.Ground] = 2.0,
                [PokemonType.Rock] = 2.0,
                [PokemonType.Dragon] = 0.5
            },
            [PokemonType.Electric] = new()
            {
                [PokemonType.Water] = 2.0,
                [PokemonType.Electric] = 0.5,
                [PokemonType.Grass] = 0.5,
                [PokemonType.Ground] = 0.0,
                [PokemonType.Flying] = 2.0,
                [PokemonType.Dragon] = 0.5
            },
            [PokemonType.Grass] = new()
            {
                [PokemonType.Fire] = 0.5,
                [PokemonType.Water] = 2.0,
                [PokemonType.Grass] = 0.5,
                [PokemonType.Poison] = 0.5,
                [PokemonType.Ground] = 2.0,
                [PokemonType.Flying] = 0.5,
                [PokemonType.Bug] = 0.5,
                [PokemonType.Rock] = 2.0,
                [PokemonType.Dragon] = 0.5,
                [PokemonType.Steel] = 0.5
            },
            [PokemonType.Ice] = new()
            {
                [PokemonType.Fire] = 0.5,
                [PokemonType.Water] = 0.5,
                [PokemonType.Grass] = 2.0,
                [PokemonType.Ice] = 0.5,
                [PokemonType.Ground] = 2.0,
                [PokemonType.Flying] = 2.0,
                [PokemonType.Dragon] = 2.0,
                [PokemonType.Steel] = 0.5
            },
            [PokemonType.Fighting] = new()
            {
                [PokemonType.Normal] = 2.0,
                [PokemonType.Ice] = 2.0,
                [PokemonType.Poison] = 0.5,
                [PokemonType.Flying] = 0.5,
                [PokemonType.Psychic] = 0.5,
                [PokemonType.Bug] = 0.5,
                [PokemonType.Rock] = 2.0,
                [PokemonType.Ghost] = 0.0,
                [PokemonType.Dark] = 2.0,
                [PokemonType.Steel] = 2.0,
                [PokemonType.Fairy] = 0.5
            },
            [PokemonType.Poison] = new()
            {
                [PokemonType.Grass] = 2.0,
                [PokemonType.Poison] = 0.5,
                [PokemonType.Ground] = 0.5,
                [PokemonType.Rock] = 0.5,
                [PokemonType.Ghost] = 0.5,
                [PokemonType.Steel] = 0.0,
                [PokemonType.Fairy] = 2.0
            },
            [PokemonType.Ground] = new()
            {
                [PokemonType.Fire] = 2.0,
                [PokemonType.Electric] = 2.0,
                [PokemonType.Grass] = 0.5,
                [PokemonType.Poison] = 2.0,
                [PokemonType.Flying] = 0.0,
                [PokemonType.Bug] = 0.5,
                [PokemonType.Rock] = 2.0,
                [PokemonType.Steel] = 2.0
            },
            [PokemonType.Flying] = new()
            {
                [PokemonType.Electric] = 0.5,
                [PokemonType.Grass] = 2.0,
                [PokemonType.Fighting] = 2.0,
                [PokemonType.Bug] = 2.0,
                [PokemonType.Rock] = 0.5,
                [PokemonType.Steel] = 0.5
            },
            [PokemonType.Psychic] = new()
            {
                [PokemonType.Fighting] = 2.0,
                [PokemonType.Poison] = 2.0,
                [PokemonType.Psychic] = 0.5,
                [PokemonType.Dark] = 0.0,
                [PokemonType.Steel] = 0.5
            },
            [PokemonType.Bug] = new()
            {
                [PokemonType.Fire] = 0.5,
                [PokemonType.Grass] = 2.0,
                [PokemonType.Fighting] = 0.5,
                [PokemonType.Poison] = 0.5,
                [PokemonType.Flying] = 0.5,
                [PokemonType.Psychic] = 2.0,
                [PokemonType.Ghost] = 0.5,
                [PokemonType.Dark] = 2.0,
                [PokemonType.Steel] = 0.5,
                [PokemonType.Fairy] = 0.5
            },
            [PokemonType.Rock] = new()
            {
                [PokemonType.Fire] = 2.0,
                [PokemonType.Ice] = 2.0,
                [PokemonType.Fighting] = 0.5,
                [PokemonType.Ground] = 0.5,
                [PokemonType.Flying] = 2.0,
                [PokemonType.Bug] = 2.0,
                [PokemonType.Steel] = 0.5
            },
            [PokemonType.Ghost] = new()
            {
                [PokemonType.Normal] = 0.0,
                [PokemonType.Psychic] = 2.0,
                [PokemonType.Ghost] = 2.0,
                [PokemonType.Dark] = 0.5
            },
            [PokemonType.Dragon] = new()
            {
                [PokemonType.Dragon] = 2.0,
                [PokemonType.Steel] = 0.5,
                [PokemonType.Fairy] = 0.0
            },
            [PokemonType.Dark] = new()
            {
                [PokemonType.Fighting] = 0.5,
                [PokemonType.Psychic] = 2.0,
                [PokemonType.Ghost] = 2.0,
                [PokemonType.Dark] = 0.5,
                [PokemonType.Fairy] = 0.5
            },
            [PokemonType.Steel] = new()
            {
                [PokemonType.Fire] = 0.5,
                [PokemonType.Water] = 0.5,
                [PokemonType.Electric] = 0.5,
                [PokemonType.Ice] = 2.0,
                [PokemonType.Rock] = 2.0,
                [PokemonType.Steel] = 0.5,
                [PokemonType.Fairy] = 2.0
            },
            [PokemonType.Fairy] = new()
            {
                [PokemonType.Fire] = 0.5,
                [PokemonType.Fighting] = 2.0,
                [PokemonType.Poison] = 0.5,
                [PokemonType.Dragon] = 2.0,
                [PokemonType.Dark] = 2.0,
                [PokemonType.Steel] = 0.5
            }
        };

    private static readonly Dictionary<string, PokemonType> TypeLookup =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["normal"] = PokemonType.Normal,
            ["fire"] = PokemonType.Fire,
            ["water"] = PokemonType.Water,
            ["electric"] = PokemonType.Electric,
            ["grass"] = PokemonType.Grass,
            ["ice"] = PokemonType.Ice,
            ["fighting"] = PokemonType.Fighting,
            ["poison"] = PokemonType.Poison,
            ["ground"] = PokemonType.Ground,
            ["flying"] = PokemonType.Flying,
            ["psychic"] = PokemonType.Psychic,
            ["bug"] = PokemonType.Bug,
            ["rock"] = PokemonType.Rock,
            ["ghost"] = PokemonType.Ghost,
            ["dragon"] = PokemonType.Dragon,
            ["dark"] = PokemonType.Dark,
            ["steel"] = PokemonType.Steel,
            ["fairy"] = PokemonType.Fairy
        };

    public static double GetMultiplier(string attackType, IReadOnlyList<string> defenderTypes)
    {
        if (!TryParseType(attackType, out var attackTypeEnum) ||
            defenderTypes == null ||
            defenderTypes.Count == 0 ||
            !OffensiveMatchups.TryGetValue(attackTypeEnum, out var matchup))
        {
            return 1.0;
        }

        var multiplier = 1.0;
        foreach (var defenderType in defenderTypes)
        {
            if (!TryParseType(defenderType, out var defenderTypeEnum))
            {
                continue;
            }

            if (matchup.TryGetValue(defenderTypeEnum, out var modifier))
            {
                multiplier *= modifier;
            }
        }

        return multiplier;
    }

    private static bool TryParseType(string value, out PokemonType type)
    {
        if (!string.IsNullOrWhiteSpace(value) && TypeLookup.TryGetValue(value.Trim(), out type))
        {
            return true;
        }

        type = PokemonType.Unknown;
        return false;
    }
}
