using System.Globalization;

namespace ElsaMina.Core.Utils;

public static class LongExtensions
{
    public static string ToReadableDataSize(this long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] sizes = ["B", "KB", "MB", "GB", "TB", "PB"];
        double len = bytes;
        var order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024.0;
        }

        return $"{len.ToString("0.##", CultureInfo.InvariantCulture)} {sizes[order]}";
    }
}