using System.Text;
using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Utils;
using Newtonsoft.Json;

namespace ElsaMina.Commands.Teams;

public static class ShowdownTeamsUtils
{
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
            }
            else if (currentSet == null)
            {
                currentSet = new PokemonSet
                {
                    Name = string.Empty,
                    Species = string.Empty,
                    Gender = string.Empty
                };

                team.Add(currentSet);
                var atIndex = line.LastIndexOf(" @ ", StringComparison.Ordinal);
                if (atIndex != -1)
                {
                    currentSet.Item = line.Substring(atIndex + 3);
                    if (currentSet.Item.ToLowerAlphaNum() == "noitem")
                    {
                        currentSet.Item = string.Empty;
                    }

                    line = line.Substring(0, atIndex);
                }

                if (line.Length >= 4 && line.Substring(line.Length - 4) == " (M)")
                {
                    currentSet.Gender = "M";
                    line = line.Substring(0, line.Length - 4);
                }

                if (line.Length >= 4 && line.Substring(line.Length - 4) == " (F)")
                {
                    currentSet.Gender = "F";
                    line = line.Substring(0, line.Length - 4);
                }

                var parenIndex = line.LastIndexOf(" (", StringComparison.Ordinal);

                if (line.Length >= 1 && line.Substring(line.Length - 1) == ")" && parenIndex != -1)
                {
                    line = line.Substring(0, line.Length - 1);
                    currentSet.Species = line.Substring(parenIndex + 2); // TODO : dex
                    line = line.Substring(0, parenIndex);
                    currentSet.Name = line;
                }
                else
                {
                    currentSet.Species = line; // TODO : dex
                    currentSet.Name = string.Empty;
                }
            }
            else if (line.Length > 7 && line.Substring(0, 7) == "Trait: ")
            {
                line = line.Substring(7);
                currentSet.Ability = line;
            }
            else if (line.Length > 9 && line.Substring(0, 9) == "Ability: ")
            {
                line = line.Substring(9);
                currentSet.Ability = line;
            }
            else if (line == "Shiny: Yes")
            {
                currentSet.IsShiny = true;
            }
            else if (line.Length > 7 && line.Substring(0, 7) == "Level: ")
            {
                line = line.Substring(7);
                currentSet.Level = int.Parse(line);
            }
            else if (line.Length > 11 && line.Substring(0, 11) == "Happiness: ")
            {
                line = line.Substring(11);
                currentSet.Happiness = int.Parse(line);
            }
            else if (line.Length > 10 && line.Substring(0, 10) == "Pokeball: ")
            {
                line = line.Substring(10);
                currentSet.Pokeball = line;
            }
            else if (line.Length > 14 && line.Substring(0, 14) == "Hidden Power: ")
            {
                line = line.Substring(14);
                currentSet.HiddenPowerType = line;
            }
            else if (line.Length > 11 && line.Substring(0, 11) == "Tera Type: ")
            {
                line = line.Substring(11);
                currentSet.TeraType = line;
            }
            else if (line.Length > 15 && line.Substring(0, 15) == "Dynamax Level: ")
            {
                line = line.Substring(15);
                currentSet.DynamaxLevel = int.Parse(line);
            }
            else if (line == "Gigantamax: Yes")
            {
                currentSet.IsGigantamax = true;
            }
            else if (line.Length > 5 && line.Substring(0, 5) == "EVs: ")
            {
                line = line.Substring(5);
                var evLines = line.Split("/");
                currentSet.EffortValues = new Dictionary<string, int>
                {
                    ["hp"] = 0,
                    ["atk"] = 0,
                    ["def"] = 0,
                    ["spa"] = 0,
                    ["spd"] = 0,
                    ["spe"] = 0,
                };
                foreach (var untrimmedEvLine in evLines)
                {
                    var evLine = untrimmedEvLine.Trim();
                    var spaceIndex = evLine.IndexOf(' ');
                    if (spaceIndex == -1)
                    {
                        continue;
                    }

                    var statId = BATTLE_STAT_IDS[evLine.Substring(spaceIndex + 1)];
                    var statVal = int.Parse(evLine.Substring(0, spaceIndex));
                    if (string.IsNullOrEmpty(statId))
                    {
                        continue;
                    }

                    currentSet.EffortValues[statId] = statVal;
                }
            }
            else if (line.Length > 5 && line.Substring(0, 5) == "IVs: ")
            {
                line = line.Substring(5);
                var ivLines = line.Split(" / ");
                currentSet.IndividualValues = new Dictionary<string, int>
                {
                    ["hp"] = 31,
                    ["atk"] = 31,
                    ["def"] = 31,
                    ["spa"] = 31,
                    ["spd"] = 31,
                    ["spe"] = 31,
                };
                foreach (var untrimmedIvLine in ivLines)
                {
                    var ivLine = untrimmedIvLine.Trim();
                    var spaceIndex = ivLine.IndexOf(' ');
                    if (spaceIndex == -1)
                    {
                        continue;
                    }

                    var statId = BATTLE_STAT_IDS[ivLine.Substring(spaceIndex + 1)];
                    var statVal = int.Parse(ivLine.Substring(0, spaceIndex));
                    if (string.IsNullOrEmpty(statId))
                    {
                        continue;
                    }

                    currentSet.IndividualValues[statId] = statVal;
                }
            }
            else if (NATURE_REGEX.IsMatch(line))
            {
                var natureIndex = line.IndexOf(" Nature", StringComparison.Ordinal);
                if (natureIndex == -1)
                {
                    natureIndex = line.IndexOf(" nature", StringComparison.Ordinal);
                }

                if (natureIndex == -1)
                {
                    continue;
                }

                line = line.Substring(0, natureIndex);
                if (!string.IsNullOrEmpty(line))
                {
                    currentSet.Nature = line;
                }
            }
            else if (line.Length > 0 && line.Substring(0, 1) == "-" || line.Substring(0, 1) == "~")
            {
                line = line.Substring(1);
                if (line.Substring(0, 1) == " ")
                {
                    line = line.Substring(1);
                }

                if (currentSet.Moves == null)
                {
                    currentSet.Moves = new List<string>();
                }

                // TODO: hidden power

                currentSet.Moves.Add(line);
            }
        }

        return team;
    }

    public static string GetTeamExport(IEnumerable<PokemonSet> sets)
    {
        return string.Join("\n\n", sets.Select(GetSetExport));
    }

    public static string GetSetExport(PokemonSet set)
    {
        var builder = new StringBuilder();
        if (set.Name != null && set.Name != set.Species)
        {
            builder.Append($"{set.Name} ({set.Species})");
        }
        else
        {
            builder.Append(set.Species);
        }

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

        var firstEv = true;
        if (set.EffortValues != null)
        {
            foreach (var (key, battleStatValue) in BATTLE_STAT_NAMES)
            {
                if (!set.EffortValues.TryGetValue(key, out var effortValue) || effortValue == 0)
                {
                    continue;
                }

                if (firstEv)
                {
                    builder.Append("EVs: ");
                    firstEv = false;
                }
                else
                {
                    builder.Append(" / ");
                }

                builder.Append($"{set.EffortValues[key]} {battleStatValue}");
            }
        }

        if (!firstEv)
        {
            builder.AppendLine();
        }

        if (set.Nature != null)
        {
            builder.AppendLine($"{set.Nature} Nature ");
        }

        var firstIv = true;
        if (set.IndividualValues != null)
        {
            foreach (var (key, value) in BATTLE_STAT_NAMES)
            {
                if (set.IndividualValues[key] == 31)
                {
                    continue;
                }

                if (firstIv)
                {
                    builder.Append("IVs: ");
                    firstIv = false;
                }
                else
                {
                    builder.Append(" / ");
                }

                builder.Append($"{set.IndividualValues[key]} {value}");
            }
        }

        if (!firstIv)
        {
            builder.AppendLine();
        }

        if (set.Moves != null)
        {
            foreach (var setMove in set.Moves)
            {
                var move = setMove;
                if (move.Length > 13 && move.Substring(0, 13) == "Hidden Power ")
                {
                    move = $"{move.Substring(0, 13)} [{move.Substring(13)}]";
                }

                if (!string.IsNullOrEmpty(move))
                {
                    builder.AppendLine($"- {move}");
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static IReadOnlyList<PokemonSet> UnpackTeam(string buf)
    {
        if (string.IsNullOrEmpty(buf))
            return [];

        var team = new List<PokemonSet>();
        var i = 0;
        var lastI = -1;

        while (i < buf.Length)
        {
            var set = new PokemonSet();
            team.Add(set);

            // name
            var j = buf.IndexOf('|', i);
            if (j < 0) break;
            var name = buf.Substring(i, j - i);
            i = j + 1;

            // species
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            var speciesStr = buf.Substring(i, j - i);
            set.Species = string.IsNullOrEmpty(speciesStr) ? name : speciesStr;
            if (set.Species != name && !string.IsNullOrEmpty(name))
                set.Name = name;
            i = j + 1;

            // item
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            set.Item = buf.Substring(i, j - i);
            i = j + 1;

            // ability
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            set.Ability = buf.Substring(i, j - i);
            i = j + 1;

            // moves
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            set.Moves = buf.Substring(i, j - i).Split(',').ToList();
            i = j + 1;

            // nature
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            var nature = buf.Substring(i, j - i);
            if (nature != "undefined")
                set.Nature = nature;
            i = j + 1;

            // evs
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            if (j != i)
            {
                var evString = buf.Substring(i, j - i);
                if (evString.Length > 5)
                {
                    var evs = evString.Split(',');
                    set.EffortValues = new Dictionary<string, int>
                    {
                        ["hp"] = int.TryParse(evs[0], out var evHp) ? evHp : 0,
                        ["atk"] = int.TryParse(evs[1], out var evAtk) ? evAtk : 0,
                        ["def"] = int.TryParse(evs[2], out var evDef) ? evDef : 0,
                        ["spa"] = int.TryParse(evs[3], out var evSpa) ? evSpa : 0,
                        ["spd"] = int.TryParse(evs[4], out var evSpd) ? evSpd : 0,
                        ["spe"] = int.TryParse(evs[5], out var evSpe) ? evSpe : 0,
                    };
                }
                else if (evString == "0")
                {
                    set.EffortValues = new Dictionary<string, int>
                        { ["hp"] = 0, ["atk"] = 0, ["def"] = 0, ["spa"] = 0, ["spd"] = 0, ["spe"] = 0 };
                }
            }

            i = j + 1;

            // gender
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            if (i != j)
                set.Gender = buf.Substring(i, j - i);
            i = j + 1;

            // ivs
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            if (j != i)
            {
                var ivs = buf.Substring(i, j - i).Split(',');
                set.IndividualValues = new Dictionary<string, int>
                {
                    ["hp"] = ivs[0] == "" ? 31 : int.Parse(ivs[0]),
                    ["atk"] = ivs[1] == "" ? 31 : int.Parse(ivs[1]),
                    ["def"] = ivs[2] == "" ? 31 : int.Parse(ivs[2]),
                    ["spa"] = ivs[3] == "" ? 31 : int.Parse(ivs[3]),
                    ["spd"] = ivs[4] == "" ? 31 : int.Parse(ivs[4]),
                    ["spe"] = ivs[5] == "" ? 31 : int.Parse(ivs[5]),
                };
            }

            i = j + 1;

            // shiny
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            if (i != j)
                set.IsShiny = true;
            i = j + 1;

            // level
            j = buf.IndexOf('|', i);
            if (j < 0) break;
            if (i != j)
                set.Level = int.Parse(buf.Substring(i, j - i));
            i = j + 1;

            // happiness and misc (comma-separated, terminated by ] or end of string)
            j = buf.IndexOf(']', i);
            string[] misc = null;
            if (j < 0)
            {
                if (i < buf.Length)
                    misc = buf.Substring(i).Split(',', 6);
            }
            else
            {
                if (i != j)
                    misc = buf.Substring(i, j - i).Split(',', 6);
            }

            if (misc != null)
            {
                set.Happiness = misc.Length > 0 && !string.IsNullOrEmpty(misc[0]) ? int.Parse(misc[0]) : -1;
                set.HiddenPowerType = misc.Length > 1 && !string.IsNullOrEmpty(misc[1]) ? misc[1] : null;
                set.Pokeball = misc.Length > 2 && !string.IsNullOrEmpty(misc[2]) ? misc[2] : null;
                set.IsGigantamax = misc.Length > 3 && !string.IsNullOrEmpty(misc[3]);
                set.DynamaxLevel = misc.Length > 4 && !string.IsNullOrEmpty(misc[4]) ? int.Parse(misc[4]) : -1;
                set.TeraType = misc.Length > 5 && !string.IsNullOrEmpty(misc[5]) ? misc[5] : null;
            }

            i = j + 1;
            if (j < 0 || i <= lastI) break;
            lastI = i;
        }

        return team;
    }

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

            // name
            buf.Append(!string.IsNullOrEmpty(set.Name) ? set.Name : set.Species);

            // species
            var speciesId = set.Species.ToLowerAlphaNum();
            var nameId = (!string.IsNullOrEmpty(set.Name) ? set.Name : set.Species).ToLowerAlphaNum();
            buf.Append('|').Append(nameId == speciesId ? string.Empty : speciesId);

            // item
            buf.Append('|').Append(set.Item?.ToLowerAlphaNum());

            // ability
            buf.Append('|').Append(set.Ability?.ToLowerAlphaNum());

            // moves
            buf.Append('|');
            if (set.Moves != null)
            {
                var first = true;
                foreach (var move in set.Moves)
                {
                    var moveId = move.ToLowerAlphaNum();
                    if (!first && string.IsNullOrEmpty(moveId))
                    {
                        continue;
                    }

                    buf.Append(first ? string.Empty : ",").Append(moveId);
                    first = false;

                    if (moveId.StartsWith("hiddenpower") && moveId.Length > 11)
                    {
                        hasHiddenPower = true;
                    }
                }
            }

            // nature
            buf.Append('|').Append(set.Nature);

            // evs
            if (set.EffortValues != null)
            {
                var evString = string.Join(",", new[] { "hp", "atk", "def", "spa", "spd", "spe" }
                    .Select(stat => set.EffortValues.TryGetValue(stat, out var value) ? value.ToString() : string.Empty));

                if (evString == ",,,,,")
                {
                    buf.Append('|');
                    if (set.EffortValues.TryGetValue("hp", out var hp) && hp == 0)
                    {
                        buf.Append('0');
                    }
                }
                else
                {
                    buf.Append('|').Append(evString);
                }
            }
            else
            {
                buf.Append('|');
            }

            // gender
            buf.Append('|').Append(set.Gender);

            // ivs
            if (set.IndividualValues != null)
            {
                var ivString = string.Join(",", new[] { "hp", "atk", "def", "spa", "spd", "spe" }
                    .Select(stat => !set.IndividualValues.TryGetValue(stat, out var value) || value == 31
                        ? string.Empty
                        : value.ToString()));

                buf.Append('|').Append(ivString == ",,,,," ? string.Empty : ivString);
            }
            else
            {
                buf.Append('|');
            }

            // shiny
            buf.Append('|').Append(set.IsShiny ? "S" : string.Empty);

            // level
            buf.Append('|').Append(set.Level != 0 && set.Level != 100 ? set.Level.ToString() : string.Empty);

            // happiness
            buf.Append('|').Append(set.Happiness >= 0 && set.Happiness != 255 ? set.Happiness.ToString() : string.Empty);

            if (!string.IsNullOrEmpty(set.Pokeball)
                || (!string.IsNullOrEmpty(set.HiddenPowerType) && !hasHiddenPower)
                || set.IsGigantamax
                || (set.DynamaxLevel >= 0 && set.DynamaxLevel != 10)
                || !string.IsNullOrEmpty(set.TeraType))
            {
                buf.Append(',').Append(!hasHiddenPower ? set.HiddenPowerType : string.Empty);
                buf.Append(',').Append(set.Pokeball?.ToLowerAlphaNum());
                buf.Append(',').Append(set.IsGigantamax ? "G" : string.Empty);
                buf.Append(',').Append(set.DynamaxLevel >= 0 && set.DynamaxLevel != 10 ? set.DynamaxLevel.ToString() : string.Empty);
                buf.Append(',').Append(set.TeraType);
            }
        }

        return buf.ToString();
    }

    public static string TeamExportToJson(string export)
    {
        return JsonConvert.SerializeObject(DeserializeTeamExport(export));
    }
}