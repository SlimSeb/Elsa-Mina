using System.Text.RegularExpressions;

namespace ElsaMina.Core.Utils;

public class RomanNumeralSuffixComparer : IComparer<string>
{
    public static readonly RomanNumeralSuffixComparer INSTANCE = new();

    private static readonly Regex CANONICAL_ROMAN_NUMERAL_REGEX =
        new("^M{0,3}(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$",
            RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    public int Compare(string first, string second)
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return -1;
        }

        if (second == null)
        {
            return 1;
        }

        var (firstPrefix, firstNumeral) = SplitOnRomanNumeralSuffix(first);
        var (secondPrefix, secondNumeral) = SplitOnRomanNumeralSuffix(second);

        if (firstNumeral != secondNumeral)
        {
            return firstNumeral.CompareTo(secondNumeral);
        }

        var prefixComparison = string.Compare(firstPrefix, secondPrefix, StringComparison.InvariantCultureIgnoreCase);
        return prefixComparison != 0
            ? prefixComparison
            : string.Compare(first, second, StringComparison.InvariantCulture);
    }

    /// <summary>
    /// Splits a string into the part before its trailing roman numeral and that numeral's value.
    /// </summary>
    private static (string Prefix, int Numeral) SplitOnRomanNumeralSuffix(string text)
    {
        var trimmed = text.TrimEnd();
        var numeralStart = trimmed.Length;
        while (numeralStart > 0 && IsRomanDigit(trimmed[numeralStart - 1]))
        {
            numeralStart--;
        }

        var hasNumeral = numeralStart < trimmed.Length;
        var isStandaloneWord = numeralStart == 0 || char.IsWhiteSpace(trimmed[numeralStart - 1]);
        if (!hasNumeral || !isStandaloneWord)
        {
            return (trimmed, 0);
        }

        var numeral = trimmed[numeralStart..].ToUpperInvariant();
        return CANONICAL_ROMAN_NUMERAL_REGEX.IsMatch(numeral)
            ? (trimmed[..numeralStart].TrimEnd(), ToArabicNumeral(numeral))
            : (trimmed, 0);
    }

    private static int ToArabicNumeral(string numeral)
    {
        var total = 0;
        for (var i = 0; i < numeral.Length; i++)
        {
            var digit = GetRomanDigitValue(numeral[i]);
            var isSubtractive = i + 1 < numeral.Length && digit < GetRomanDigitValue(numeral[i + 1]);
            total += isSubtractive ? -digit : digit;
        }

        return total;
    }

    private static bool IsRomanDigit(char character) => GetRomanDigitValue(character) > 0;

    private static int GetRomanDigitValue(char character) => char.ToUpperInvariant(character) switch
    {
        'I' => 1,
        'V' => 5,
        'X' => 10,
        'L' => 50,
        'C' => 100,
        'D' => 500,
        'M' => 1000,
        _ => 0
    };
}
