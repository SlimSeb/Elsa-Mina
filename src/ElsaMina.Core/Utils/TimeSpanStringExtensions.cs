using System.Globalization;
using System.Text.RegularExpressions;

namespace ElsaMina.Core.Utils;

public static class TimeSpanStringExtensions
{
    private static readonly Regex TIME_SPAN_REGEX =
        new(@"(\d+(?:\.\d+)?)\s*(
            milliseconds?|ms|
            seconds?|secs?|s|
            minutes?|mins?|m|
            hours?|hrs?|h|
            days?|d|
            weeks?|w
        )",
            RegexOptions.IgnoreCase
            | RegexOptions.Compiled
            | RegexOptions.IgnorePatternWhitespace, Constants.REGEX_MATCH_TIMEOUT);
    
    private static readonly Dictionary<string, Func<double, TimeSpan>> TIME_UNITS =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // milliseconds
            ["ms"] = TimeSpan.FromMilliseconds,
            ["millisecond"] = TimeSpan.FromMilliseconds,
            ["milliseconds"] = TimeSpan.FromMilliseconds,

            // seconds
            ["s"] = TimeSpan.FromSeconds,
            ["sec"] = TimeSpan.FromSeconds,
            ["secs"] = TimeSpan.FromSeconds,
            ["second"] = TimeSpan.FromSeconds,
            ["seconds"] = TimeSpan.FromSeconds,

            // minutes
            ["m"] = TimeSpan.FromMinutes,
            ["min"] = TimeSpan.FromMinutes,
            ["mins"] = TimeSpan.FromMinutes,
            ["minute"] = TimeSpan.FromMinutes,
            ["minutes"] = TimeSpan.FromMinutes,

            // hours
            ["h"] = TimeSpan.FromHours,
            ["hr"] = TimeSpan.FromHours,
            ["hrs"] = TimeSpan.FromHours,
            ["hour"] = TimeSpan.FromHours,
            ["hours"] = TimeSpan.FromHours,

            // days
            ["d"] = TimeSpan.FromDays,
            ["day"] = TimeSpan.FromDays,
            ["days"] = TimeSpan.FromDays,

            // weeks
            ["w"] = v => TimeSpan.FromDays(v * 7),
            ["week"] = v => TimeSpan.FromDays(v * 7),
            ["weeks"] = v => TimeSpan.FromDays(v * 7)
        };

    public static TimeSpan? ToTimeSpan(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var matches = TIME_SPAN_REGEX.Matches(input);
        if (matches.Count == 0)
        {
            return null;
        }

        var total = TimeSpan.Zero;

        foreach (var groups in matches.Select(match => match.Groups))
        {
            if (!double.TryParse(
                    groups[1].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return null;
            }

            var unit = groups[2].Value;

            if (!TIME_UNITS.TryGetValue(unit, out var factory))
            {
                return null;
            }

            total += factory(value);
        }

        if (TIME_SPAN_REGEX.Replace(input, string.Empty).Trim().Length > 0)
        {
            return null;
        }

        return total;
    }
}