using System.Text;
using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Utils;
using System.Text.Json;

namespace ElsaMina.Commands.Teams;

public static class ShowdownTeamsUtils
{
    private static readonly string[] STAT_KEYS = ["hp", "atk", "def", "spa", "spd", "spe"];

    private const int DEFAULT_INDIVIDUAL_VALUE = 31;

    private static readonly Dictionary<string, string> BATTLE_STAT_IDS = new()
    {
        ["HP"] = "hp",
        ["hp"] = "hp",
        ["Atk"] = "atk",
        ["atk"] = "atk",
        ["Def"] = "def",
        ["def"] = "def",
        ["SpA"] = "spa",
        ["SAtk"] = "spa",
        ["SpAtk"] = "spa",
        ["spa"] = "spa",
        ["SpD"] = "spd",
        ["SDef"] = "spd",
        ["SpDef"] = "spd",
        ["spd"] = "spd",
        ["Spe"] = "spe",
        ["Spd"] = "spe",
        ["spe"] = "spe"
    };

    private static readonly Dictionary<string, string> BATTLE_STAT_NAMES = new()
    {
        ["hp"] = "HP",
        ["atk"] = "Atk",
        ["def"] = "Def",
        ["spa"] = "SpA",
        ["spd"] = "SpD",
        ["spe"] = "Spe"
    };

    private static readonly Regex NATURE_REGEX =
        new("^[A-Za-z]+ (N|n)ature", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    #region Team export deserialization

    public static IReadOnlyList<PokemonSet> DeserializeTeamExport(string export)
    {
        var team = new List<PokemonSet>();
        PokemonSet currentSet = null;

        foreach (var teamLine in export.Split("\n"))
        {
            var line = teamLine.Trim();
            if (line == string.Empty || line == "---")
            {
                currentSet = null;
                continue;
            }

            if (currentSet == null)
            {
                currentSet = CreateSetFromHeaderLine(line);
                team.Add(currentSet);
                continue;
            }

            ParseSetDetailLine(currentSet, line);
        }

        return team;
    }

    private static PokemonSet CreateSetFromHeaderLine(string headerLine)
    {
        var set = new PokemonSet
        {
            Name = string.Empty,
            Species = string.Empty,
            Gender = string.Empty
        };

        var remainder = ParseItemFromHeaderLine(set, headerLine);
        remainder = ParseGenderFromHeaderLine(set, remainder);
        ParseNameAndSpeciesFromHeaderLine(set, remainder);

        return set;
    }

    private static string ParseItemFromHeaderLine(PokemonSet set, string line)
    {
        var atIndex = line.LastIndexOf(" @ ", StringComparison.Ordinal);
        if (atIndex == -1)
        {
            return line;
        }

        set.Item = line.Substring(atIndex + 3);
        if (set.Item.ToLowerAlphaNum() == "noitem")
        {
            set.Item = string.Empty;
        }

        return line.Substring(0, atIndex);
    }

    private static string ParseGenderFromHeaderLine(PokemonSet set, string line)
    {
        var remainder = line;
        if (remainder.Length >= 4 && remainder.Substring(remainder.Length - 4) == " (M)")
        {
            set.Gender = "M";
            remainder = remainder.Substring(0, remainder.Length - 4);
        }

        if (remainder.Length >= 4 && remainder.Substring(remainder.Length - 4) == " (F)")
        {
            set.Gender = "F";
            remainder = remainder.Substring(0, remainder.Length - 4);
        }

        return remainder;
    }

    private static void ParseNameAndSpeciesFromHeaderLine(PokemonSet set, string line)
    {
        var parenIndex = line.LastIndexOf(" (", StringComparison.Ordinal);
        if (line.Length < 1 || line.Substring(line.Length - 1) != ")" || parenIndex == -1)
        {
            set.Species = line; // TODO : dex
            set.Name = string.Empty;
            return;
        }

        var withoutClosingParenthesis = line.Substring(0, line.Length - 1);
        set.Species = withoutClosingParenthesis.Substring(parenIndex + 2); // TODO : dex
        set.Name = withoutClosingParenthesis.Substring(0, parenIndex);
    }

    private static void ParseSetDetailLine(PokemonSet set, string line)
    {
        if (TryParseTextField(set, line))
        {
            return;
        }

        if (TryParseNumericField(set, line))
        {
            return;
        }

        if (TryParseBooleanField(set, line))
        {
            return;
        }

        if (TryParseStatsField(set, line))
        {
            return;
        }

        if (TryParseNature(set, line))
        {
            return;
        }

        TryParseMove(set, line);
    }

    private static bool TryParseTextField(PokemonSet set, string line)
    {
        if (TryGetPrefixedValue(line, "Trait: ", out var trait))
        {
            set.Ability = trait;
            return true;
        }

        if (TryGetPrefixedValue(line, "Ability: ", out var ability))
        {
            set.Ability = ability;
            return true;
        }

        if (TryGetPrefixedValue(line, "Pokeball: ", out var pokeball))
        {
            set.Pokeball = pokeball;
            return true;
        }

        if (TryGetPrefixedValue(line, "Hidden Power: ", out var hiddenPowerType))
        {
            set.HiddenPowerType = hiddenPowerType;
            return true;
        }

        if (TryGetPrefixedValue(line, "Tera Type: ", out var teraType))
        {
            set.TeraType = teraType;
            return true;
        }

        return false;
    }

    private static bool TryParseNumericField(PokemonSet set, string line)
    {
        if (TryGetPrefixedValue(line, "Level: ", out var level))
        {
            set.Level = int.Parse(level);
            return true;
        }

        if (TryGetPrefixedValue(line, "Happiness: ", out var happiness))
        {
            set.Happiness = int.Parse(happiness);
            return true;
        }

        if (TryGetPrefixedValue(line, "Dynamax Level: ", out var dynamaxLevel))
        {
            set.DynamaxLevel = int.Parse(dynamaxLevel);
            return true;
        }

        return false;
    }

    private static bool TryParseBooleanField(PokemonSet set, string line)
    {
        switch (line)
        {
            case "Shiny: Yes":
                set.IsShiny = true;
                return true;
            case "Gigantamax: Yes":
                set.IsGigantamax = true;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseStatsField(PokemonSet set, string line)
    {
        if (TryGetPrefixedValue(line, "EVs: ", out var effortValues))
        {
            set.EffortValues = ParseStats(effortValues.Split("/"), 0);
            return true;
        }

        if (TryGetPrefixedValue(line, "IVs: ", out var individualValues))
        {
            set.IndividualValues = ParseStats(individualValues.Split(" / "), DEFAULT_INDIVIDUAL_VALUE);
            return true;
        }

        return false;
    }

    private static Dictionary<string, int> ParseStats(IEnumerable<string> statEntries, int defaultValue)
    {
        var stats = STAT_KEYS.ToDictionary(statKey => statKey, _ => defaultValue);

        foreach (var untrimmedEntry in statEntries)
        {
            var entry = untrimmedEntry.Trim();
            var spaceIndex = entry.IndexOf(' ');
            if (spaceIndex == -1)
            {
                continue;
            }

            var statId = BATTLE_STAT_IDS[entry.Substring(spaceIndex + 1)];
            var statValue = int.Parse(entry.Substring(0, spaceIndex));
            if (string.IsNullOrEmpty(statId))
            {
                continue;
            }

            stats[statId] = statValue;
        }

        return stats;
    }

    private static bool TryParseNature(PokemonSet set, string line)
    {
        if (!NATURE_REGEX.IsMatch(line))
        {
            return false;
        }

        var natureIndex = line.IndexOf(" Nature", StringComparison.Ordinal);
        if (natureIndex == -1)
        {
            natureIndex = line.IndexOf(" nature", StringComparison.Ordinal);
        }

        if (natureIndex == -1)
        {
            return true;
        }

        var nature = line.Substring(0, natureIndex);
        if (!string.IsNullOrEmpty(nature))
        {
            set.Nature = nature;
        }

        return true;
    }

    private static void TryParseMove(PokemonSet set, string line)
    {
        var isMoveLine = (line.Length > 0 && line.Substring(0, 1) == "-") || line.Substring(0, 1) == "~";
        if (!isMoveLine)
        {
            return;
        }

        var move = line.Substring(1);
        if (move.Substring(0, 1) == " ")
        {
            move = move.Substring(1);
        }

        set.Moves ??= new List<string>();

        // TODO: hidden power

        set.Moves.Add(move);
    }

    private static bool TryGetPrefixedValue(string line, string prefix, out string value)
    {
        if (line.Length > prefix.Length && line.Substring(0, prefix.Length) == prefix)
        {
            value = line.Substring(prefix.Length);
            return true;
        }

        value = null;
        return false;
    }

    #endregion

    #region Team export serialization

    public static string GetTeamExport(IEnumerable<PokemonSet> sets)
    {
        return string.Join("\n\n", sets.Select(GetSetExport));
    }

    public static string GetSetExport(PokemonSet set)
    {
        var builder = new StringBuilder();

        AppendExportedHeaderLine(builder, set);
        AppendExportedAttributes(builder, set);
        AppendExportedEffortValues(builder, set);
        AppendExportedNature(builder, set);
        AppendExportedIndividualValues(builder, set);
        AppendExportedMoves(builder, set);

        return builder.ToString();
    }

    private static void AppendExportedHeaderLine(StringBuilder builder, PokemonSet set)
    {
        builder.Append(set.Name != null && set.Name != set.Species
            ? $"{set.Name} ({set.Species})"
            : set.Species);

        if (set.Gender == "M")
        {
            builder.Append(" (M)");
        }

        if (set.Gender == "F")
        {
            builder.Append(" (F)");
        }

        if (set.Item != null)
        {
            builder.Append($" @ {set.Item}");
        }

        builder.AppendLine();
    }

    private static void AppendExportedAttributes(StringBuilder builder, PokemonSet set)
    {
        if (set.Ability != null)
        {
            builder.AppendLine($"Ability: {set.Ability} ");
        }

        if (set.Level != 0 && set.Level != 100)
        {
            builder.AppendLine($"Level: {set.Level} ");
        }

        if (set.IsShiny)
        {
            builder.AppendLine("Shiny: Yes ");
        }

        if (set.Happiness >= 0 && set.Happiness != 255)
        {
            builder.AppendLine($"Happiness: {set.Happiness} ");
        }

        if (set.Pokeball != null)
        {
            builder.AppendLine($"Pokeball: {set.Pokeball} ");
        }

        if (set.HiddenPowerType != null)
        {
            builder.AppendLine($"Hidden Power: {set.HiddenPowerType} ");
        }

        if (set.DynamaxLevel >= 0)
        {
            builder.AppendLine($"Dynamax Level: {set.DynamaxLevel} ");
        }

        if (set.IsGigantamax)
        {
            builder.AppendLine("Gigantamax: Yes ");
        }
    }

    private static void AppendExportedEffortValues(StringBuilder builder, PokemonSet set)
    {
        if (set.EffortValues == null)
        {
            return;
        }

        var isFirstEffortValue = true;
        foreach (var (statKey, statName) in BATTLE_STAT_NAMES)
        {
            if (!set.EffortValues.TryGetValue(statKey, out var effortValue) || effortValue == 0)
            {
                continue;
            }

            builder.Append(isFirstEffortValue ? "EVs: " : " / ");
            isFirstEffortValue = false;
            builder.Append($"{effortValue} {statName}");
        }

        if (!isFirstEffortValue)
        {
            builder.AppendLine();
        }
    }

    private static void AppendExportedNature(StringBuilder builder, PokemonSet set)
    {
        if (set.Nature != null)
        {
            builder.AppendLine($"{set.Nature} Nature ");
        }
    }

    private static void AppendExportedIndividualValues(StringBuilder builder, PokemonSet set)
    {
        if (set.IndividualValues == null)
        {
            return;
        }

        var isFirstIndividualValue = true;
        foreach (var (statKey, statName) in BATTLE_STAT_NAMES)
        {
            var individualValue = set.IndividualValues[statKey];
            if (individualValue == DEFAULT_INDIVIDUAL_VALUE)
            {
                continue;
            }

            builder.Append(isFirstIndividualValue ? "IVs: " : " / ");
            isFirstIndividualValue = false;
            builder.Append($"{individualValue} {statName}");
        }

        if (!isFirstIndividualValue)
        {
            builder.AppendLine();
        }
    }

    private static void AppendExportedMoves(StringBuilder builder, PokemonSet set)
    {
        if (set.Moves == null)
        {
            return;
        }

        foreach (var setMove in set.Moves)
        {
            var move = FormatExportedMove(setMove);
            if (!string.IsNullOrEmpty(move))
            {
                builder.AppendLine($"- {move}");
            }
        }

        builder.AppendLine();
    }

    private static string FormatExportedMove(string move)
    {
        return move.Length > 13 && move.Substring(0, 13) == "Hidden Power "
            ? $"{move.Substring(0, 13)} [{move.Substring(13)}]"
            : move;
    }

    #endregion

    #region Packed team parsing

    public static IReadOnlyList<PokemonSet> UnpackTeam(string buf)
    {
        if (string.IsNullOrEmpty(buf))
            return [];

        var team = new List<PokemonSet>();
        var index = 0;
        var lastIndex = -1;

        while (index < buf.Length)
        {
            var set = new PokemonSet();
            team.Add(set);

            if (!TryUnpackIdentityFields(buf, set, ref index) || !TryUnpackBattleFields(buf, set, ref index))
            {
                break;
            }

            var miscSeparatorIndex = UnpackMiscFields(buf, set, ref index);
            if (miscSeparatorIndex < 0 || index <= lastIndex)
            {
                break;
            }

            lastIndex = index;
        }

        return team;
    }

    private static bool TryUnpackIdentityFields(string buf, PokemonSet set, ref int index)
    {
        // name and species
        if (!TryReadPackedField(buf, ref index, out var name) ||
            !TryReadPackedField(buf, ref index, out var species))
        {
            return false;
        }

        set.Species = string.IsNullOrEmpty(species) ? name : species;
        if (set.Species != name && !string.IsNullOrEmpty(name))
        {
            set.Name = name;
        }

        // item
        if (!TryReadPackedField(buf, ref index, out var item))
        {
            return false;
        }

        set.Item = item;

        // ability
        if (!TryReadPackedField(buf, ref index, out var ability))
        {
            return false;
        }

        set.Ability = ability;

        // moves
        if (!TryReadPackedField(buf, ref index, out var moves))
        {
            return false;
        }

        set.Moves = moves.Split(',').ToList();
        return true;
    }

    private static bool TryUnpackBattleFields(string buf, PokemonSet set, ref int index)
    {
        // nature
        if (!TryReadPackedField(buf, ref index, out var nature))
        {
            return false;
        }

        if (nature != "undefined")
        {
            set.Nature = nature;
        }

        // evs
        if (!TryReadPackedField(buf, ref index, out var effortValues))
        {
            return false;
        }

        ApplyPackedEffortValues(set, effortValues);

        // gender
        if (!TryReadPackedField(buf, ref index, out var gender))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(gender))
        {
            set.Gender = gender;
        }

        // ivs
        if (!TryReadPackedField(buf, ref index, out var individualValues))
        {
            return false;
        }

        ApplyPackedIndividualValues(set, individualValues);

        // shiny
        if (!TryReadPackedField(buf, ref index, out var shiny))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(shiny))
        {
            set.IsShiny = true;
        }

        // level
        if (!TryReadPackedField(buf, ref index, out var level))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(level))
        {
            set.Level = int.Parse(level);
        }

        return true;
    }

    private static bool TryReadPackedField(string buf, ref int index, out string value)
    {
        var separatorIndex = buf.IndexOf('|', index);
        if (separatorIndex < 0)
        {
            value = null;
            return false;
        }

        value = buf.Substring(index, separatorIndex - index);
        index = separatorIndex + 1;
        return true;
    }

    private static void ApplyPackedEffortValues(PokemonSet set, string packedEffortValues)
    {
        if (packedEffortValues.Length > 5)
        {
            var effortValues = packedEffortValues.Split(',');
            set.EffortValues = new Dictionary<string, int>
            {
                ["hp"] = ParsePackedEffortValue(effortValues[0]),
                ["atk"] = ParsePackedEffortValue(effortValues[1]),
                ["def"] = ParsePackedEffortValue(effortValues[2]),
                ["spa"] = ParsePackedEffortValue(effortValues[3]),
                ["spd"] = ParsePackedEffortValue(effortValues[4]),
                ["spe"] = ParsePackedEffortValue(effortValues[5])
            };
            return;
        }

        if (packedEffortValues == "0")
        {
            set.EffortValues = STAT_KEYS.ToDictionary(statKey => statKey, _ => 0);
        }
    }

    private static int ParsePackedEffortValue(string value)
    {
        return int.TryParse(value, out var effortValue) ? effortValue : 0;
    }

    private static void ApplyPackedIndividualValues(PokemonSet set, string packedIndividualValues)
    {
        if (string.IsNullOrEmpty(packedIndividualValues))
        {
            return;
        }

        var individualValues = packedIndividualValues.Split(',');
        set.IndividualValues = new Dictionary<string, int>
        {
            ["hp"] = ParsePackedIndividualValue(individualValues[0]),
            ["atk"] = ParsePackedIndividualValue(individualValues[1]),
            ["def"] = ParsePackedIndividualValue(individualValues[2]),
            ["spa"] = ParsePackedIndividualValue(individualValues[3]),
            ["spd"] = ParsePackedIndividualValue(individualValues[4]),
            ["spe"] = ParsePackedIndividualValue(individualValues[5])
        };
    }

    private static int ParsePackedIndividualValue(string value)
    {
        return value == "" ? DEFAULT_INDIVIDUAL_VALUE : int.Parse(value);
    }

    // happiness and misc (comma-separated, terminated by ] or end of string)
    private static int UnpackMiscFields(string buf, PokemonSet set, ref int index)
    {
        var separatorIndex = buf.IndexOf(']', index);
        var misc = ReadPackedMiscFields(buf, index, separatorIndex);
        if (misc != null)
        {
            ApplyPackedMiscFields(set, misc);
        }

        index = separatorIndex + 1;
        return separatorIndex;
    }

    private static string[] ReadPackedMiscFields(string buf, int index, int separatorIndex)
    {
        if (separatorIndex < 0)
        {
            return index < buf.Length ? buf.Substring(index).Split(',', 6) : null;
        }

        return index != separatorIndex ? buf.Substring(index, separatorIndex - index).Split(',', 6) : null;
    }

    private static void ApplyPackedMiscFields(PokemonSet set, string[] misc)
    {
        set.Happiness = misc.Length > 0 && !string.IsNullOrEmpty(misc[0]) ? int.Parse(misc[0]) : -1;
        set.HiddenPowerType = misc.Length > 1 && !string.IsNullOrEmpty(misc[1]) ? misc[1] : null;
        set.Pokeball = misc.Length > 2 && !string.IsNullOrEmpty(misc[2]) ? misc[2] : null;
        set.IsGigantamax = misc.Length > 3 && !string.IsNullOrEmpty(misc[3]);
        set.DynamaxLevel = misc.Length > 4 && !string.IsNullOrEmpty(misc[4]) ? int.Parse(misc[4]) : -1;
        set.TeraType = misc.Length > 5 && !string.IsNullOrEmpty(misc[5]) ? misc[5] : null;
    }

    #endregion

    #region Team packing

    public static string PackTeam(IReadOnlyList<PokemonSet> team)
    {
        if (team == null || team.Count == 0)
        {
            return string.Empty;
        }

        var buf = new StringBuilder();
        var hasHiddenPower = false;

        foreach (var set in team)
        {
            if (buf.Length > 0)
            {
                buf.Append(']');
            }

            AppendPackedIdentity(buf, set);
            hasHiddenPower |= AppendPackedMoves(buf, set);
            buf.Append('|').Append(set.Nature);
            AppendPackedEffortValues(buf, set);
            buf.Append('|').Append(set.Gender);
            AppendPackedIndividualValues(buf, set);
            AppendPackedMiscFields(buf, set, hasHiddenPower);
        }

        return buf.ToString();
    }

    private static void AppendPackedIdentity(StringBuilder buf, PokemonSet set)
    {
        // name
        var displayedName = !string.IsNullOrEmpty(set.Name) ? set.Name : set.Species;
        buf.Append(displayedName);

        // species
        var speciesId = set.Species.ToLowerAlphaNum();
        var nameId = displayedName.ToLowerAlphaNum();
        buf.Append('|').Append(nameId == speciesId ? string.Empty : speciesId);

        // item
        buf.Append('|').Append(set.Item?.ToLowerAlphaNum());

        // ability
        buf.Append('|').Append(set.Ability?.ToLowerAlphaNum());
    }

    private static bool AppendPackedMoves(StringBuilder buf, PokemonSet set)
    {
        buf.Append('|');
        if (set.Moves == null)
        {
            return false;
        }

        var hasHiddenPower = false;
        var isFirstMove = true;
        foreach (var move in set.Moves)
        {
            var moveId = move.ToLowerAlphaNum();
            if (!isFirstMove && string.IsNullOrEmpty(moveId))
            {
                continue;
            }

            buf.Append(isFirstMove ? string.Empty : ",").Append(moveId);
            isFirstMove = false;

            if (moveId.StartsWith("hiddenpower") && moveId.Length > 11)
            {
                hasHiddenPower = true;
            }
        }

        return hasHiddenPower;
    }

    private static void AppendPackedEffortValues(StringBuilder buf, PokemonSet set)
    {
        if (set.EffortValues == null)
        {
            buf.Append('|');
            return;
        }

        var effortValuesString = string.Join(",", STAT_KEYS
            .Select(stat => set.EffortValues.TryGetValue(stat, out var value) ? value.ToString() : string.Empty));

        if (effortValuesString != ",,,,,")
        {
            buf.Append('|').Append(effortValuesString);
            return;
        }

        buf.Append('|');
        if (set.EffortValues.TryGetValue("hp", out var hitPoints) && hitPoints == 0)
        {
            buf.Append('0');
        }
    }

    private static void AppendPackedIndividualValues(StringBuilder buf, PokemonSet set)
    {
        if (set.IndividualValues == null)
        {
            buf.Append('|');
            return;
        }

        var individualValuesString = string.Join(",", STAT_KEYS
            .Select(stat => !set.IndividualValues.TryGetValue(stat, out var value) || value == DEFAULT_INDIVIDUAL_VALUE
                ? string.Empty
                : value.ToString()));

        buf.Append('|').Append(individualValuesString == ",,,,," ? string.Empty : individualValuesString);
    }

    private static void AppendPackedMiscFields(StringBuilder buf, PokemonSet set, bool hasHiddenPower)
    {
        // shiny
        buf.Append('|').Append(set.IsShiny ? "S" : string.Empty);

        // level
        buf.Append('|').Append(set.Level != 0 && set.Level != 100 ? set.Level.ToString() : string.Empty);

        // happiness
        buf.Append('|').Append(set.Happiness >= 0 && set.Happiness != 255 ? set.Happiness.ToString() : string.Empty);

        if (!HasPackedExtraFields(set, hasHiddenPower))
        {
            return;
        }

        buf.Append(',').Append(!hasHiddenPower ? set.HiddenPowerType : string.Empty);
        buf.Append(',').Append(set.Pokeball?.ToLowerAlphaNum());
        buf.Append(',').Append(set.IsGigantamax ? "G" : string.Empty);
        buf.Append(',')
            .Append(set.DynamaxLevel >= 0 && set.DynamaxLevel != 10 ? set.DynamaxLevel.ToString() : string.Empty);
        buf.Append(',').Append(set.TeraType);
    }

    private static bool HasPackedExtraFields(PokemonSet set, bool hasHiddenPower)
    {
        return !string.IsNullOrEmpty(set.Pokeball)
               || (!string.IsNullOrEmpty(set.HiddenPowerType) && !hasHiddenPower)
               || set.IsGigantamax
               || (set.DynamaxLevel >= 0 && set.DynamaxLevel != 10)
               || !string.IsNullOrEmpty(set.TeraType);
    }

    #endregion

    public static string TeamExportToJson(string export)
    {
        return JsonSerializer.Serialize(DeserializeTeamExport(export));
    }
}
