using System.Text.RegularExpressions;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Dolls;

/// <summary>
/// Turns the Google Drive folder and file names into doll sizes, ids and display names.
/// Folder names carry the size ("Grandes 32x32"), file names carry the doll identity
/// ("riolu_debout_face_deux_pieds_32x32.png").
/// </summary>
public static partial class DollCatalogueNaming
{
    private const int MIN_SIZE = 8;
    private const int MAX_SIZE = 128;

    public static bool TryParseSize(string folderName, out int size)
    {
        size = 0;
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        var match = SizeRegex().Match(folderName);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var parsedSize))
        {
            return false;
        }

        if (parsedSize is < MIN_SIZE or > MAX_SIZE)
        {
            return false;
        }

        size = parsedSize;
        return true;
    }

    public static string ToDollId(string fileName)
    {
        return ToBaseName(fileName).ToLowerAlphaNum();
    }

    public static string ToDisplayName(string fileName)
    {
        var name = ToBaseName(fileName)
            .Replace('_', ' ')
            .Replace('-', ' ');
        name = WhitespaceRegex().Replace(name, " ").Trim();

        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Drops what carries no information in a catalogue that only holds dolls: the extension,
    /// the "Doll_" prefix the sprites are named with, and the size already given by the folder.
    /// The generation suffix is kept, since it is what tells two sprites of a same Pokémon apart.
    /// </summary>
    private static string ToBaseName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        name = SizeSuffixRegex().Replace(name, string.Empty).Trim();
        return DollPrefixRegex().Replace(name, string.Empty).Trim();
    }

    [GeneratedRegex(@"(\d+)\s*[xX×]\s*\d+", RegexOptions.Compiled)]
    private static partial Regex SizeRegex();

    [GeneratedRegex(@"[_\-\s]*\d+\s*[xX×]\s*\d+\s*$", RegexOptions.Compiled)]
    private static partial Regex SizeSuffixRegex();

    [GeneratedRegex(@"^doll[_\-\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DollPrefixRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
