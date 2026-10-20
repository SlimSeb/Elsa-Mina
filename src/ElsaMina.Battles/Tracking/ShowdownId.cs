using System.Text;

namespace ElsaMina.Battles.Tracking;

public static class ShowdownId
{
    /// <summary>
    /// poke-env's <c>to_id_str</c>: keeps the characters Python's <c>str.isalnum</c> accepts (any Unicode
    /// letter or number, not only [a-z0-9]) and lowercases them. "Mr. Mime" gives "mrmime".
    /// </summary>
    public static string ToId(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name.Where(character => char.IsLetter(character) || char.IsNumber(character)))
        {
            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>
    /// poke-env's <c>Move.retrieve_id</c>: a move id, with Return, Frustration and Hidden Power variants
    /// ("return102", "hiddenpowerfire60") collapsed to their base move.
    /// </summary>
    public static string ToMoveId(string moveName)
    {
        var id = ToId(moveName);
        if (id.StartsWith("return", StringComparison.Ordinal))
        {
            return "return";
        }

        if (id.StartsWith("frustration", StringComparison.Ordinal))
        {
            return "frustration";
        }

        return id.StartsWith("hiddenpower", StringComparison.Ordinal) ? "hiddenpower" : id;
    }
}
