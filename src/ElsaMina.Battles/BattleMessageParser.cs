using ElsaMina.Battles.Dtos;
using ElsaMina.Logging;
using Newtonsoft.Json;

namespace ElsaMina.Battles;

public class BattleMessageParser : IBattleMessageParser
{
    public bool TryApplyMessage(string[] parts, string roomId, BattleContext context, out BattleMessageResult result)
    {
        result = new BattleMessageResult(BattleMessageType.None);

        if (parts.Length < 2)
        {
            return false;
        }

        if (parts[1] == "start")
        {
            result = new BattleMessageResult(BattleMessageType.BattleStarted);
            return true;
        }

        if (parts[1] is "win" or "tie")
        {
            context.IsBattleOver = true;
            var winnerName = parts[1] == "win" && parts.Length >= 3 ? parts[2] : null;
            var isTie = parts[1] == "tie";
            result = new BattleMessageResult(BattleMessageType.BattleEnded, winnerName, isTie);
            return true;
        }

        // Opponent state tracking - handled before the request check since they all return false
        if (TryApplyOpponentMessage(parts, context))
        {
            return false;
        }

        if (parts[1] != "request" || parts.Length < 3)
        {
            return false;
        }

        var requestJson = parts[2];
        if (string.IsNullOrWhiteSpace(requestJson) || requestJson == "null")
        {
            return false;
        }

        try
        {
            var battleState = JsonConvert.DeserializeObject<BattleStateDto>(requestJson);
            if (battleState == null)
            {
                return false;
            }

            context.Rqid = battleState.Rqid;
            context.Wait = battleState.Wait;
            context.TeamPreview = battleState.TeamPreview;
            context.NoCancel = battleState.NoCancel;
            context.SideName = battleState.Side?.Name ?? "";
            context.SideId = battleState.Side?.Id ?? "";
            context.ForceSwitchSlots = ParseForceSwitch(battleState);
            context.SidePokemon = ParseSidePokemon(battleState);
            context.ActiveSlots = ParseActiveSlots(battleState);

            result = new BattleMessageResult(BattleMessageType.RequestUpdated);
            return true;
        }
        catch (JsonException exception)
        {
            Log.Error(exception, "Failed to parse battle request");
            return false;
        }
    }

    private static bool TryApplyOpponentMessage(string[] parts, BattleContext context)
    {
        // SideId is set by the first |request| message. Until then we don't know which side is
        // ours, so every switch message would be misidentified as opponent - skip entirely.
        if (string.IsNullOrEmpty(context.SideId))
        {
            return false;
        }

        switch (parts[1])
        {
            // Team preview: |poke|p1|Garchomp, L80, M|item
            case "poke" when parts.Length >= 4:
            {
                var playerSide = parts[2];
                if (playerSide == context.OpponentSideId)
                {
                    var (species, level, gender) = ParseDetails(parts[3]);
                    var pokemon = GetOrCreateOpponentPokemon(context, species);
                    pokemon.Level = level;
                    pokemon.Gender = gender;
                }
                return true;
            }

            // Switch / drag / replace (Zoroark illusion broken):
            // |switch|p1a: Garchomp|Garchomp, L80, M|88/100
            case "switch" or "drag" or "replace" when parts.Length >= 5:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                var (species, level, gender) = ParseDetails(parts[3]);
                ParseHpStatus(parts[4], out var hpPercent, out var status, out var fainted);

                foreach (var pokemon in context.OpponentPokemon)
                {
                    pokemon.IsActive = false;
                }

                var switched = GetOrCreateOpponentPokemon(context, species);
                switched.Level = level;
                switched.Gender = gender;
                switched.HpPercent = hpPercent;
                switched.Status = status;
                switched.IsActive = !fainted;
                switched.IsFainted = fainted;
                switched.Boosts.Clear();
                return true;
            }

            // Forme change: |detailschange|p1a: Mimikyu|Mimikyu-Busted, L79, F
            case "detailschange" when parts.Length >= 4:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                var oldSpecies = ExtractSpeciesFromIdent(parts[2]);
                var (newSpecies, _, _) = ParseDetails(parts[3]);
                var pokemon = context.OpponentPokemon.FirstOrDefault(p => p.Species == oldSpecies);
                if (pokemon != null)
                {
                    pokemon.Species = newSpecies;
                }

                return true;
            }

            // Move used: |move|p1a: Garchomp|Earthquake|p2a: Mimikyu
            case "move" when parts.Length >= 4:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                var species = ExtractSpeciesFromIdent(parts[2]);
                var moveName = parts[3];
                var pokemon = GetOrCreateOpponentPokemon(context, species);
                EnsureActive(context, pokemon);
                pokemon.LastUsedMove = moveName;
                return true;
            }

            // HP damage: |-damage|p1a: Garchomp|64/100
            case "-damage" when parts.Length >= 4:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                var species = ExtractSpeciesFromIdent(parts[2]);
                ParseHpStatus(parts[3], out var hpPercent, out var status, out var fainted);
                var pokemon = GetOrCreateOpponentPokemon(context, species);
                EnsureActive(context, pokemon);
                pokemon.HpPercent = hpPercent;
                pokemon.IsFainted = fainted;
                if (fainted)
                {
                    pokemon.IsActive = false;
                }

                if (!string.IsNullOrEmpty(status))
                {
                    pokemon.Status = status;
                }

                return true;
            }

            // HP heal: |-heal|p1a: Garchomp|80/100
            case "-heal" when parts.Length >= 4:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                var species = ExtractSpeciesFromIdent(parts[2]);
                ParseHpStatus(parts[3], out var hpPercent, out _, out _);
                var pokemon = GetOrCreateOpponentPokemon(context, species);
                EnsureActive(context, pokemon);
                pokemon.HpPercent = hpPercent;

                return true;
            }

            // Fainted: |faint|p1a: Garchomp
            case "faint" when parts.Length >= 3:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                var species = ExtractSpeciesFromIdent(parts[2]);
                var pokemon = context.OpponentPokemon.FirstOrDefault(p => p.Species == species);
                if (pokemon != null)
                {
                    pokemon.HpPercent = 0;
                    pokemon.IsFainted = true;
                    pokemon.IsActive = false;
                }

                return true;
            }

            // Status applied: |-status|p1a: Garchomp|brn
            case "-status" when parts.Length >= 4:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                var species = ExtractSpeciesFromIdent(parts[2]);
                var pokemon = context.OpponentPokemon.FirstOrDefault(p => p.Species == species);
                if (pokemon != null)
                {
                    pokemon.Status = parts[3];
                }

                return true;
            }

            // Status cured: |-curestatus|p1a: Garchomp|brn
            case "-curestatus" when parts.Length >= 3:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                var species = ExtractSpeciesFromIdent(parts[2]);
                var pokemon = context.OpponentPokemon.FirstOrDefault(p => p.Species == species);
                if (pokemon != null)
                {
                    pokemon.Status = "";
                }

                return true;
            }

            // Stat boost: |-boost|p1a: Garchomp|atk|2
            case "-boost" when parts.Length >= 5:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                ApplyBoost(context, parts[2], parts[3], parts[4], negative: false);
                return true;
            }

            // Stat unboost: |-unboost|p1a: Garchomp|atk|2
            case "-unboost" when parts.Length >= 5:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                ApplyBoost(context, parts[2], parts[3], parts[4], negative: true);
                return true;
            }

            // Clear a single pokemon's boosts: |-clearboost|p1a: Garchomp
            case "-clearboost" when parts.Length >= 3:
            {
                if (!IsOpponentIdent(parts[2], context.SideId))
                {
                    return true;
                }

                var species = ExtractSpeciesFromIdent(parts[2]);
                var pokemon = context.OpponentPokemon.FirstOrDefault(p => p.Species == species);
                pokemon?.Boosts.Clear();
                return true;
            }

            // Clear all boosts on both sides: |-clearallboost|
            case "-clearallboost":
            {
                foreach (var pokemon in context.OpponentPokemon)
                {
                    pokemon.Boosts.Clear();
                }

                return true;
            }

            default:
                return false;
        }
    }

    private static bool IsOpponentIdent(string ident, string ourSideId)
    {
        // ident format: "p1a: Garchomp" - first two chars are the side id
        return ident.Length >= 2 && ident[..2] != ourSideId;
    }

    private static string ExtractSpeciesFromIdent(string ident)
    {
        // "p1a: Garchomp" → "Garchomp"
        var colonIndex = ident.IndexOf(':');
        return colonIndex < 0 || colonIndex + 2 >= ident.Length ? "" : ident[(colonIndex + 2)..];
    }

    private static (string species, int level, string gender) ParseDetails(string details)
    {
        // Format: "Garchomp, L80, M" or "Garchomp, L80" or "Garchomp, M" or "Garchomp"
        // Level 100 omits the L prefix in some cases: "Sunflora, M"
        var tokens = details.Split(", ");
        var species = tokens[0];
        var level = 100;
        var gender = "";

        foreach (var token in tokens.AsSpan(1))
        {
            if (token.StartsWith('L') && int.TryParse(token.AsSpan(1), out var lvl))
            {
                level = lvl;
            }
            else if (token is "M" or "F")
            {
                gender = token;
            }
        }

        return (species, level, gender);
    }

    private static void ParseHpStatus(string hpStatus, out double hpPercent, out string status, out bool fainted)
    {
        hpPercent = 0;
        status = "";
        fainted = false;

        if (string.IsNullOrWhiteSpace(hpStatus))
        {
            return;
        }

        // Format: "88/100", "88/100 brn", "0 fnt"
        var spaceIndex = hpStatus.IndexOf(' ');
        var hpPart = spaceIndex >= 0 ? hpStatus[..spaceIndex] : hpStatus;
        var statusPart = spaceIndex >= 0 ? hpStatus[(spaceIndex + 1)..] : "";

        if (statusPart == "fnt")
        {
            fainted = true;
            return;
        }

        status = statusPart;

        var slashIndex = hpPart.IndexOf('/');
        if (slashIndex < 0)
        {
            return;
        }

        if (int.TryParse(hpPart.AsSpan(0, slashIndex), out var current) &&
            int.TryParse(hpPart.AsSpan(slashIndex + 1), out var max) && max > 0)
        {
            hpPercent = (double)current / max * 100.0;
        }
    }

    // Marks a pokemon as active if no active opponent exists yet (e.g., initial switch was missed).
    // Does not override the active flag if another opponent is already marked active.
    private static void EnsureActive(BattleContext context, OpponentPokemonState pokemon)
    {
        if (!pokemon.IsActive && context.OpponentPokemon.All(p => !p.IsActive))
        {
            pokemon.IsActive = true;
        }
    }

    private static OpponentPokemonState GetOrCreateOpponentPokemon(BattleContext context, string species)
    {
        var existing = context.OpponentPokemon.FirstOrDefault(p => p.Species == species);
        if (existing != null)
        {
            return existing;
        }

        var created = new OpponentPokemonState { Species = species };
        context.OpponentPokemon.Add(created);
        return created;
    }

    private static void ApplyBoost(BattleContext context, string ident, string stat, string amountStr, bool negative)
    {
        var species = ExtractSpeciesFromIdent(ident);
        var pokemon = context.OpponentPokemon.FirstOrDefault(p => p.Species == species);
        if (pokemon == null || !int.TryParse(amountStr, out var amount))
        {
            return;
        }

        var delta = negative ? -amount : amount;
        pokemon.Boosts.TryGetValue(stat, out var current);
        pokemon.Boosts[stat] = Math.Clamp(current + delta, -6, 6);
    }

    private static List<bool> ParseForceSwitch(BattleStateDto root)
    {
        return root.ForceSwitch == null || root.ForceSwitch.Count == 0 ? [] : root.ForceSwitch;
    }

    private static List<BattlePokemonState> ParseSidePokemon(BattleStateDto root)
    {
        if (root.Side?.Pokemon == null || root.Side.Pokemon.Count == 0)
        {
            return [];
        }

        var results = new List<BattlePokemonState>(root.Side.Pokemon.Count);
        foreach (var pokemon in root.Side.Pokemon)
        {
            var condition = pokemon.Condition ?? "";
            var isFainted = condition.Contains("fnt", StringComparison.OrdinalIgnoreCase);
            ParseConditionHp(condition, out var currentHp, out var maxHp);

            results.Add(new BattlePokemonState
            {
                Ident = pokemon.Ident,
                Details = pokemon.Details,
                Condition = condition,
                CurrentHp = currentHp,
                MaxHp = maxHp,
                IsActive = pokemon.Active,
                IsFainted = isFainted,
                Stats = new BattlePokemonStats(
                    pokemon.Stats.Atk,
                    pokemon.Stats.Def,
                    pokemon.Stats.Spa,
                    pokemon.Stats.Spd,
                    pokemon.Stats.Spe),
                Moves = pokemon.Moves,
                BaseAbility = pokemon.BaseAbility,
                Ability = pokemon.Ability,
                Item = pokemon.Item,
                Pokeball = pokemon.Pokeball,
                TeraType = pokemon.TeraType,
                Terastallized = pokemon.Terastallized,
                Commanding = pokemon.Commanding,
                Reviving = pokemon.Reviving
            });
        }

        return results;
    }

    private static List<BattleActiveSlot> ParseActiveSlots(BattleStateDto root)
    {
        if (root.Active == null || root.Active.Count == 0)
        {
            return [];
        }

        var slots = new List<BattleActiveSlot>(root.Active.Count);
        foreach (var activeSlot in root.Active)
        {
            if (activeSlot?.Moves == null || activeSlot.Moves.Count == 0)
            {
                slots.Add(new BattleActiveSlot());
                continue;
            }

            var moves = new List<BattleMoveState>(activeSlot.Moves.Count);
            foreach (var move in activeSlot.Moves)
            {
                moves.Add(new BattleMoveState
                {
                    Name = move?.Name ?? "",
                    Id = move?.Id ?? "",
                    Pp = move?.Pp ?? 0,
                    MaxPp = move?.MaxPp ?? 0,
                    Target = move?.Target ?? "",
                    IsDisabled = move?.Disabled ?? false
                });
            }

            slots.Add(new BattleActiveSlot { Moves = moves, CanTerastallize = activeSlot.CanTerastallize, Trapped = activeSlot.Trapped });
        }

        return slots;
    }

    private static void ParseConditionHp(string condition, out int currentHp, out int maxHp)
    {
        currentHp = 0;
        maxHp = 0;

        if (string.IsNullOrWhiteSpace(condition))
        {
            return;
        }

        // Condition format: "168/216" or "168/216 brn" or "0 fnt"
        var hpPart = condition.Split(' ')[0];
        var slashIndex = hpPart.IndexOf('/');
        if (slashIndex < 0)
        {
            return;
        }

        int.TryParse(hpPart.AsSpan(0, slashIndex), out currentHp);
        int.TryParse(hpPart.AsSpan(slashIndex + 1), out maxHp);
    }
}
