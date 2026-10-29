using System.Globalization;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.Formats;

public class FormatsManager : IFormatsManager
{
    private const string CUSTOM_RULES_SEPARATOR = "@@@";
    private const string RANDOM = "random";

    // The |formats| list's flags (server/rooms.ts): 1 = the format generates the teams
    private const int GENERATED_TEAMS_FLAG = 1;

    private readonly SortedSet<string> _formats = [];
    private readonly HashSet<string> _generatedTeamFormats = [];

    public IEnumerable<string> Formats => _formats;

    public void ParseFormats(string[] formats)
    {
        if (formats == null)
        {
            return;
        }
        foreach (var format in formats)
        {
            if (!format.StartsWith("[Gen"))
            {
                continue;
            }

            var parts = format.Split(",");
            _formats.Add(parts[0]);
            if (parts.Length > 1 && int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out var flags) && (flags & GENERATED_TEAMS_FLAG) != 0)
            {
                _generatedTeamFormats.Add(parts[0].ToLowerAlphaNum());
            }
        }
    }

    public string GetCleanFormat(string formatId)
    {
        return _formats.FirstOrDefault(format => format.ToLowerAlphaNum() == formatId) ?? formatId;
    }

    public bool HasGeneratedTeams(string format)
    {
        var formatId = format.Split(CUSTOM_RULES_SEPARATOR)[0].ToLowerAlphaNum();
        if (_formats.Any(name => name.ToLowerAlphaNum() == formatId))
        {
            return _generatedTeamFormats.Contains(formatId);
        }

        // Before the server's format list arrives
        return formatId.Contains(RANDOM, StringComparison.Ordinal);
    }
}
