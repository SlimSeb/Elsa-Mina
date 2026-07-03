using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Logging;
using Lusamine.DamageCalc;
using Lusamine.DamageCalc.Data;

namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// Builds Lusamine.DamageCalc Pokemon instances from the battle state tracked by the bot.
/// </summary>
public static class CalcPokemonFactory
{
    public static readonly IGeneration Generation = DataIndex.Create(9);

    public static bool TryBuildOurPokemon(BattlePokemonState state, out Pokemon pokemon)
    {
        pokemon = null;
        var species = ExtractSpeciesFromDetails(state.Details);
        var level = ExtractLevelFromDetails(state.Details);

        try
        {
            pokemon = new Pokemon(Generation, species, new State.Pokemon
            {
                Level = level,
                Item = string.IsNullOrEmpty(state.Item) ? null : state.Item,
                Ability = string.IsNullOrEmpty(state.Ability) ? null : state.Ability,
                TeraType = string.IsNullOrEmpty(state.Terastallized) ? null : state.Terastallized,
                Status = ExtractStatus(state.Condition),
                CurHP = state.CurrentHp > 0 ? state.CurrentHp : null
            });

            // Override with the exact in-battle stats from the request JSON
            pokemon.RawStats = new StatsTable
            {
                Hp = state.MaxHp,
                Atk = state.Stats.Atk,
                Def = state.Stats.Def,
                Spa = state.Stats.SpA,
                Spd = state.Stats.SpD,
                Spe = state.Stats.Spe
            };

            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to build calc Pokemon for {Species}", species);
            return false;
        }
    }

    public static bool TryBuildOpponentPokemon(OpponentPokemonState state, out Pokemon pokemon,
        PredictedSpread spread = null)
    {
        pokemon = null;

        try
        {
            pokemon = new Pokemon(Generation, state.Species, new State.Pokemon
            {
                Level = state.Level,
                Status = string.IsNullOrEmpty(state.Status) ? null : state.Status,
                Boosts = BuildBoostsInput(state.Boosts),
                // Applying the predicted nature + EV spread makes the opponent's speed tier, damage
                // and bulk realistic instead of the calc's default 0-EV neutral spread
                Nature = spread == null || string.IsNullOrEmpty(spread.Nature) ? null : spread.Nature,
                Evs = spread == null ? null : BuildEvsInput(spread)
            });

            // Apply tracked HP percentage - derive actual HP from computed max (EVs affect max HP)
            var maxHp = pokemon.MaxHP(false);
            pokemon.OriginalCurHP = (int)Math.Max(1, Math.Round(maxHp * state.HpPercent / 100.0));

            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to build calc Pokemon for opponent {Species}", state.Species);
            return false;
        }
    }

    public static string ExtractSpeciesFromDetails(string details)
    {
        var commaIndex = details.IndexOf(',');
        return commaIndex < 0 ? details : details[..commaIndex];
    }

    public static int ExtractLevelFromDetails(string details)
    {
        foreach (var token in details.Split(", ").AsSpan(1))
        {
            if (token.StartsWith('L') && int.TryParse(token.AsSpan(1), out var level))
            {
                return level;
            }
        }

        return 100;
    }

    public static string ExtractStatus(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return null;
        }

        var spaceIndex = condition.IndexOf(' ');
        if (spaceIndex < 0)
        {
            return null;
        }

        var statusPart = condition[(spaceIndex + 1)..];
        return statusPart is "fnt" or "" ? null : statusPart;
    }

    private static StatsTableInput BuildEvsInput(PredictedSpread spread)
    {
        return new StatsTableInput
        {
            Hp = spread.HpEvs,
            Atk = spread.AtkEvs,
            Def = spread.DefEvs,
            Spa = spread.SpaEvs,
            Spd = spread.SpdEvs,
            Spe = spread.SpeEvs
        };
    }

    private static StatsTableInput BuildBoostsInput(Dictionary<string, int> boosts)
    {
        return new StatsTableInput
        {
            Atk = GetBoost(boosts, "atk"),
            Def = GetBoost(boosts, "def"),
            Spa = GetBoost(boosts, "spa"),
            Spd = GetBoost(boosts, "spd"),
            Spe = GetBoost(boosts, "spe")
        };
    }

    private static int? GetBoost(Dictionary<string, int> boosts, string key) =>
        boosts.TryGetValue(key, out var value) && value != 0 ? value : null;
}
