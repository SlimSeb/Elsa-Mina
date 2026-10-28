using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Tracking;

namespace ElsaMina.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// What our side knows of a battle, as search_sim.js reads it (tools/minipoke/search.py's position_of): our team from
/// the request (sets, exact HP, stats, PP), the opponent's revealed Pokémon from the tracked state, side conditions,
/// weather, terrain and the turn.
/// </summary>
public static partial class SearchPositionBuilder
{
    private const string CHOICE_PREFIX = "choice";
    private const int WEATHER_TURNS = 5;
    private const int TERRAIN_TURNS = 5;
    private const int PSEUDO_WEATHER_TURNS = 5;
    private const int DEFAULT_TEAM_SIZE = 6;

    private static readonly Dictionary<string, int> DURATIONS = new()
    {
        ["reflect"] = 5, ["lightscreen"] = 5, ["auroraveil"] = 5, ["tailwind"] = 4, ["safeguard"] = 5, ["mist"] = 5
    };

    private static readonly HashSet<string> ENDLESS_WEATHERS = ["desolateland", "primordialsea", "deltastream"];
    private static readonly HashSet<string> TERRAINS = ["electricterrain", "grassyterrain", "mistyterrain", "psychicterrain"];
    private static readonly HashSet<string> PSEUDO_WEATHERS = ["trickroom", "gravity", "magicroom", "wonderroom"];
    private static readonly HashSet<string> LAYERED = ["spikes", "toxicspikes"];

    private static readonly Dictionary<string, string> VOLATILES = new()
    {
        ["SUBSTITUTE"] = "substitute", ["LEECH_SEED"] = "leechseed", ["CONFUSION"] = "confusion",
        ["TAUNT"] = "taunt", ["YAWN"] = "yawn", ["FOCUS_ENERGY"] = "focusenergy", ["SALT_CURE"] = "saltcure"
    };

    public static JsonObject Build(BattleContext context)
    {
        var state = context.Tracked;
        var ours = state.PlayerRole;
        var theirs = ours == "p1" ? "p2" : "p1";
        return new JsonObject
        {
            ["us"] = ours,
            ["turn"] = state.Turn,
            ["field"] = Field(state),
            ["sides"] = new JsonObject { [ours] = OurSide(context), [theirs] = TheirSide(state, theirs) }
        };
    }

    /// <summary>
    /// The name in a Pokémon's ident or identifier: "p1: Garchomp" -> "Garchomp".
    /// </summary>
    public static string NameOf(string ident)
    {
        var separator = ident.IndexOf(": ", StringComparison.Ordinal);
        return separator < 0 ? ident : ident[(separator + 2)..];
    }

    private static JsonObject OurSide(BattleContext context)
    {
        var state = context.Tracked;
        var activeMoves = context.ActiveSlots.Count > 0 ? context.ActiveSlots[0].Moves : [];
        var pokemon = new JsonArray();
        foreach (var request in context.SidePokemon)
        {
            var tracked = TrackedBattleState.Find(state.Team, request.Ident);
            pokemon.Add(OurPokemon(request, tracked, activeMoves));
        }

        return new JsonObject
        {
            ["pokemon"] = pokemon,
            ["size"] = context.SidePokemon.Count,
            ["teraUsed"] = state.UsedTera,
            ["conditions"] = Conditions(state.SideConditions, state.Turn)
        };
    }

    private static JsonObject OurPokemon(BattlePokemonState request, TrackedPokemon tracked,
        List<BattleMoveState> activeMoves)
    {
        var details = request.Details.Split(", ");
        var level = details.Select(part => LevelPattern().Match(part)).FirstOrDefault(match => match.Success);
        var condition = request.Condition.Split(' ');
        var status = condition.Length > 1 ? condition[1] : "";
        var moves = new JsonArray();
        foreach (var move in request.Moves)
        {
            var active = request.IsActive ? activeMoves.Find(slot => slot.Id == move) : null;
            moves.Add(new JsonObject { ["id"] = move, ["pp"] = active?.Pp });
        }

        var ability = !string.IsNullOrEmpty(request.Ability) ? request.Ability : tracked?.Ability;
        var entry = new JsonObject
        {
            ["name"] = NameOf(request.Ident),
            ["species"] = details[0],
            ["level"] = level == null ? 100 : int.Parse(level.Groups[1].Value, CultureInfo.InvariantCulture),
            ["gender"] = details.FirstOrDefault(part => part is "M" or "F") ?? "",
            ["moves"] = moves,
            ["item"] = request.Item ?? "",
            ["ability"] = ability,
            ["teraType"] = string.IsNullOrEmpty(request.TeraType) ? TypeName(tracked?.TeraType) : request.TeraType,
            ["terastallized"] = string.IsNullOrEmpty(request.Terastallized) ? null : request.Terastallized,
            ["hp"] = request.CurrentHp,
            ["maxhp"] = request.MaxHp,
            ["status"] = status == "fnt" ? "" : status,
            ["fainted"] = request.IsFainted,
            ["stats"] = new JsonObject
            {
                ["atk"] = request.Stats.Atk, ["def"] = request.Stats.Def, ["spa"] = request.Stats.SpA,
                ["spd"] = request.Stats.SpD, ["spe"] = request.Stats.Spe
            },
            ["active"] = request.IsActive
        };
        if (request.IsActive && tracked != null)
        {
            entry["boosts"] = Boosts(tracked);
            entry["activeTurns"] = tracked.ActiveTurns;
            var volatiles = Volatiles(tracked);
            var enabled = activeMoves.Where(move => !move.IsDisabled).ToList();
            if ((request.Item ?? "").StartsWith(CHOICE_PREFIX, StringComparison.Ordinal) && enabled.Count == 1 &&
                activeMoves.Count > 1)
            {
                volatiles.Add(new JsonObject { ["id"] = "choicelock", ["move"] = enabled[0].Id });
            }

            entry["volatiles"] = volatiles;
        }

        return entry;
    }

    private static JsonObject TheirSide(TrackedBattleState state, string role)
    {
        var pokemon = new JsonArray();
        foreach (var tracked in state.OpponentTeam)
        {
            pokemon.Add(TheirPokemon(tracked));
        }

        var size = state.TeamSizes.GetValueOrDefault(role, DEFAULT_TEAM_SIZE);
        return new JsonObject
        {
            ["pokemon"] = pokemon,
            ["size"] = Math.Max(size, state.OpponentTeam.Count),
            ["teraUsed"] = state.OpponentUsedTera,
            ["conditions"] = Conditions(state.OpponentSideConditions, state.Turn)
        };
    }

    private static JsonObject TheirPokemon(TrackedPokemon tracked)
    {
        var moves = new JsonArray();
        foreach (var move in tracked.Moves.Values.Take(BattleObservation.MoveCount)
                     .Where(move => !PokeEnvNames.SpecialMoves.Contains(move.Id)))
        {
            moves.Add(new JsonObject { ["id"] = move.Id, ["pp"] = move.CurrentPp });
        }

        var item = tracked.Item == TrackedPokemon.UNKNOWN_ITEM ? null : tracked.Item ?? "";
        var tera = tracked.Terastallized ? tracked.TeraType : null;
        return new JsonObject
        {
            ["name"] = NameOf(tracked.Identifier),
            ["species"] = tracked.Species,
            ["forme"] = tracked.Species,
            ["level"] = tracked.Level,
            ["gender"] = "",
            ["moves"] = moves,
            ["ability"] = tracked.Ability == null ? null : ShowdownId.ToId(tracked.Ability),
            ["item"] = item,
            ["teraType"] = tera?.ToLowerInvariant(),
            ["terastallized"] = TypeName(tera),
            ["hpFraction"] = tracked.CurrentHpFraction,
            ["status"] = tracked.Fainted || tracked.Status == null ? "" : tracked.Status.ToLowerInvariant(),
            ["fainted"] = tracked.Fainted,
            ["active"] = tracked.Active,
            ["boosts"] = tracked.Active ? Boosts(tracked) : new JsonObject(),
            ["activeTurns"] = tracked.Active ? tracked.ActiveTurns : 0,
            ["volatiles"] = tracked.Active ? Volatiles(tracked) : new JsonArray()
        };
    }

    private static JsonObject Boosts(TrackedPokemon tracked)
    {
        var boosts = new JsonObject();
        foreach (var (stat, value) in tracked.Boosts.Where(pair => pair.Value != 0))
        {
            boosts[stat] = value;
        }

        return boosts;
    }

    private static JsonArray Volatiles(TrackedPokemon tracked)
    {
        var volatiles = new JsonArray();
        foreach (var effect in tracked.Effects.Where(VOLATILES.ContainsKey))
        {
            volatiles.Add(new JsonObject { ["id"] = VOLATILES[effect] });
        }

        return volatiles;
    }

    private static JsonObject Conditions(Dictionary<string, int> conditions, int turn)
    {
        var json = new JsonObject();
        foreach (var (name, value) in conditions)
        {
            var id = ToShowdownId(name);
            if (LAYERED.Contains(id))
            {
                json[id] = value;
            }
            else
            {
                json[id] = DURATIONS.TryGetValue(id, out var duration) ? Math.Max(1, duration - (turn - value)) : 1;
            }
        }

        return json;
    }

    private static JsonObject Field(TrackedBattleState state)
    {
        var pseudo = new JsonObject();
        var field = new JsonObject { ["pseudo"] = pseudo };
        foreach (var (weather, start) in state.Weather)
        {
            var id = ToShowdownId(weather);
            field["weather"] = id;
            field["weatherTurns"] = ENDLESS_WEATHERS.Contains(id) ? 0 : Math.Max(1, WEATHER_TURNS - (state.Turn - start));
        }

        foreach (var (name, start) in state.Fields)
        {
            var id = ToShowdownId(name);
            if (TERRAINS.Contains(id))
            {
                field["terrain"] = id;
                field["terrainTurns"] = Math.Max(1, TERRAIN_TURNS - (state.Turn - start));
            }
            else if (PSEUDO_WEATHERS.Contains(id))
            {
                pseudo[id] = Math.Max(1, PSEUDO_WEATHER_TURNS - (state.Turn - start));
            }
        }

        return field;
    }

    /// <summary>
    /// poke-env's enum name of a weather, field or side condition as Showdown's id: "STEALTH_ROCK" -> "stealthrock".
    /// </summary>
    private static string ToShowdownId(string name) => name.Replace("_", "").ToLowerInvariant();

    /// <summary>
    /// poke-env's type name as Showdown writes it: "GROUND" -> "Ground".
    /// </summary>
    private static string TypeName(string type) =>
        string.IsNullOrEmpty(type) ? null : type[..1] + type[1..].ToLowerInvariant();

    [GeneratedRegex(@"^L(\d+)$")]
    private static partial Regex LevelPattern();
}
