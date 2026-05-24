using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ElsaMina.Core.Utils;

public static class StringExtensions
{
    private static readonly Regex ALPHA_NUMERIC_FILTER_REGEX = new("[^A-Za-z0-9]",
        RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex WHITESPACE_BETWEEN_TAGS_REGEX =
        new(@"\s*(<[^>]+>)\s*", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex EXCESS_WHITESPACE_BETWEEN_TAGS_REGEX =
        new(@"\s{2,}(?=<)|(?<=>)\s{2,}", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex HTML_TAG_REGEX =
        new(@"<[^>]+>", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex MULTIPLE_WHITESPACE_REGEX =
        new(@"\s{2,}", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex IMAGE_LINK_REGEX = new("(http)?s?:(//[^\"']*.(?:png|jpg|jpeg|gif|png|svg))",
        RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    public static string ToLowerAlphaNum(this string text)
    {
        return ALPHA_NUMERIC_FILTER_REGEX.Replace(text.ToLower(), string.Empty);
    }

    public static string RemoveNewlines(this string text)
    {
        return text.Replace("\n", string.Empty);
    }

    public static string RemoveWhitespacesBetweenTags(this string text)
    {
        return WHITESPACE_BETWEEN_TAGS_REGEX.Replace(text, "$1");
    }

    public static string CollapseWhitespacesBetweenTags(this string text)
    {
        return EXCESS_WHITESPACE_BETWEEN_TAGS_REGEX.Replace(text, " ");
    }

    public static string CollapseAttributeWhitespace(this string text)
    {
        return HTML_TAG_REGEX.Replace(text, match => MULTIPLE_WHITESPACE_REGEX.Replace(match.Value, " "));
    }

    public static string Capitalize(this string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text[0].ToString().ToUpper() + text[1..];
    }

    public static bool ToBoolean(this string text)
    {
        return text.Trim().ToLower() is "true" or "y" or "t" or "1" or "on";
    }

    public static string Shorten(this string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || maxLength <= 0)
        {
            return string.Empty;
        }

        var words = text.Split(' ');
        var output = new StringBuilder();
        var length = 0;

        foreach (var word in words)
        {
            if (length + word.Length > maxLength)
            {
                break;
            }

            if (output.Length > 0)
            {
                output.Append(' ');
                length++;
            }

            output.Append(word);
            length += word.Length;
        }

        if (length < text.Length)
        {
            output.Append("...");
        }

        return output.ToString();
    }

    public static string ToMd5Digest(this string text)
    {
        var stringBuilder = new StringBuilder();
        foreach (var octet in MD5.HashData(Encoding.UTF8.GetBytes(text)))
        {
            stringBuilder.Append(octet.ToString("x2").ToLower());
        }

        return stringBuilder.ToString();
    }

    public static bool IsValidImageLink(this string link) =>
        !string.IsNullOrWhiteSpace(link) && IMAGE_LINK_REGEX.IsMatch(link);

    /// <remarks>
    /// Credit : https://gist.github.com/Davidblkx/e12ab0bb2aff7fd8072632b396538560
    /// </remarks>
    public static int LevenshteinDistance(this string source, string other)
    {
        var source1Length = source.Length;
        var source2Length = other.Length;

        var matrix = new int[source1Length + 1, source2Length + 1];

        if (source1Length == 0)
        {
            return source2Length;
        }

        if (source2Length == 0)
        {
            return source1Length;
        }

        for (var i = 0; i <= source1Length; matrix[i, 0] = i++)
        {
            // Do nothing
        }

        for (var j = 0; j <= source2Length; matrix[0, j] = j++)
        {
            // Do nothing
        }

        for (var i = 1; i <= source1Length; i++)
        {
            for (var j = 1; j <= source2Length; j++)
            {
                var cost = other[j - 1] == source[i - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[source1Length, source2Length];
    }

    public static string RemoveExtension(this string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return fileName[..^extension.Length];
    }
    
    public static bool IsSingleEmoji(this string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(input);
        
        // Must be exactly one text element (grapheme cluster)
        if (!enumerator.MoveNext())
        {
            return false;
        }

        var element = enumerator.GetTextElement();
        if (enumerator.MoveNext())
        {
            return false; // more than one grapheme
        }

        return IsEmojiTextElement(element);
    }

    public static bool IsAllEmoji(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        var enumerator = StringInfo.GetTextElementEnumerator(input);
        while (enumerator.MoveNext())
        {
            if (!IsEmojiTextElement(enumerator.GetTextElement()))
                return false;
        }
        return true;
    }

    public static bool ContainsEmoji(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        var enumerator = StringInfo.GetTextElementEnumerator(input);
        while (enumerator.MoveNext())
        {
            if (IsEmojiTextElement(enumerator.GetTextElement()))
                return true;
        }
        return false;
    }

    private static bool IsEmojiTextElement(string element)
    {
        if (string.IsNullOrEmpty(element)) return false;

        var codePoint = char.ConvertToUtf32(element, 0);

        // Regional indicator letters -> flag emoji (🇦-🇿)
        if (IsRegionalIndicator(codePoint) && element.Length >= 4)
        {
            return true;
        }

        // Keycap sequences: digit/# + U+FE0F + U+20E3
        if (IsKeycapBase(codePoint) && element.Contains('\u20E3'))
        {
            return true;
        }

        return IsEmojiCodePoint(codePoint);
    }

    private static bool IsEmojiCodePoint(int cp) => cp switch
    {
        // Miscellaneous Symbols and Dingbats
        >= 0x2600 and <= 0x26FF => true,
        >= 0x2700 and <= 0x27BF => true,

        // Emoticons block
        >= 0x1F600 and <= 0x1F64F => true,

        // Misc symbols and pictographs
        >= 0x1F300 and <= 0x1F5FF => true,

        // Transport and map
        >= 0x1F680 and <= 0x1F6FF => true,

        // Supplemental symbols and pictographs
        >= 0x1F900 and <= 0x1F9FF => true,

        // Symbols and pictographs extended-A
        >= 0x1FA00 and <= 0x1FA6F => true,
        >= 0x1FA70 and <= 0x1FAFF => true,

        // Enclosed alphanumeric supplement (circled numbers, etc.)
        >= 0x1F100 and <= 0x1F1FF => true,

        // Enclosed ideographic supplement
        >= 0x1F200 and <= 0x1F2FF => true,

        // Mahjong / domino tiles
        >= 0x1F000 and <= 0x1F02F => true,
        >= 0x1F0A0 and <= 0x1F0FF => true,

        // Miscellaneous technical
        >= 0x2300 and <= 0x23FF => true,

        // Arrows and other common emoji
        >= 0x2B00 and <= 0x2BFF => true,
        >= 0x25A0 and <= 0x25FF => true,
        >= 0x2100 and <= 0x214F => true,

        // Tags block (used in flag sequences)
        >= 0xE0000 and <= 0xE007F => true,

        _ => false
    };

    private static bool IsRegionalIndicator(int cp)
        => cp is >= 0x1F1E6 and <= 0x1F1FF;

    private static bool IsKeycapBase(int cp)
        => cp is (>= '0' and <= '9') or '#' or '*';
}
