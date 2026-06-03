using System.Collections.Concurrent;
using Lusamine.DamageCalc;
using Lusamine.DamageCalc.Data;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// Translates a <see cref="CalcRequestDto"/> into Lusamine.DamageCalc domain objects and
/// runs the calculation, returning the formatted description string.
/// </summary>
public class DamageCalculator : IDamageCalculator
{
    private const int DEFAULT_GENERATION = 9;

    private static readonly ConcurrentDictionary<int, IGeneration> GENERATIONS = new();

    public string Calculate(CalcRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Attacker);
        ArgumentNullException.ThrowIfNull(request.Defender);

        if (string.IsNullOrWhiteSpace(request.Move))
        {
            throw new ArgumentException("A move is required to run a damage calculation.", nameof(request));
        }

        var generationNumber = request.Gen is >= 1 and <= 9 ? request.Gen : DEFAULT_GENERATION;
        var generation = GENERATIONS.GetOrAdd(generationNumber, DataIndex.Create);

        var attacker = BuildPokemon(generation, request.Attacker);
        var defender = BuildPokemon(generation, request.Defender);
        var move = new Move(generation, request.Move, new State.Move
        {
            IsCrit = request.IsCrit,
            Hits = request.Hits
        });
        var field = BuildField(request.Field);

        var result = Lusamine.DamageCalc.Calc.Calculate(generation, attacker, defender, move, field);
        return result.Desc();
    }

    private static Pokemon BuildPokemon(IGeneration generation, CalcPokemonDto dto)
    {
        var options = new State.Pokemon
        {
            Level = dto.Level,
            Nature = NullIfBlank(dto.Nature),
            Ability = NullIfBlank(dto.Ability),
            Item = NullIfBlank(dto.Item),
            Status = NullIfBlank(dto.Status),
            TeraType = NullIfBlank(dto.TeraType),
            Evs = ToStatsTable(dto.Evs),
            Ivs = ToStatsTable(dto.Ivs),
            Boosts = ToStatsTable(dto.Boosts)
        };

        return new Pokemon(generation, dto.Name, options);
    }

    private static Field BuildField(CalcFieldDto dto)
    {
        if (dto == null)
        {
            return new Field();
        }

        return new Field(new State.Field
        {
            GameType = string.IsNullOrWhiteSpace(dto.GameType) ? GameTypes.Singles : dto.GameType,
            Weather = NullIfBlank(dto.Weather),
            Terrain = NullIfBlank(dto.Terrain),
            AttackerSide = ToSide(dto.AttackerSide),
            DefenderSide = ToSide(dto.DefenderSide)
        });
    }

    private static State.Side ToSide(CalcSideDto dto)
    {
        if (dto == null)
        {
            return null;
        }

        return new State.Side
        {
            IsReflect = dto.IsReflect,
            IsLightScreen = dto.IsLightScreen,
            IsAuroraVeil = dto.IsAuroraVeil,
            IsSR = dto.IsStealthRock,
            Spikes = dto.Spikes,
            IsHelpingHand = dto.IsHelpingHand,
            IsTailwind = dto.IsTailwind,
            IsFriendGuard = dto.IsFriendGuard
        };
    }

    private static StatsTableInput ToStatsTable(CalcStatsDto dto)
    {
        if (dto == null)
        {
            return null;
        }

        return new StatsTableInput
        {
            Hp = dto.Hp,
            Atk = dto.Atk,
            Def = dto.Def,
            Spa = dto.Spa,
            Spd = dto.Spd,
            Spe = dto.Spe
        };
    }

    private static string NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
