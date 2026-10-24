namespace ElsaMina.Battles.Strategies.MiniPokeV2;

/// <summary>
/// history.py: what happened earlier in a battle, read from its protocol log
/// (<see cref="Tracking.TrackedBattleState.ProtocolLines"/>, split on '|' like poke-env's battle._replay_data).
/// "The previous turn" is the lines between the last two |turn| lines, so a decision taken mid-turn (a
/// replacement after a faint) still looks at the last complete turn. A |move| line with a "[from]" tag after the
/// move (Outrage's lock, Sleep Talk's call, Magic Bounce...) is not a choice.
/// </summary>
public static class ProtocolHistory
{
    private const string FROM_TAG = "[from]";
    private const string STRUGGLE = "Struggle";
    private static readonly HashSet<string> ENTRANCES = ["switch", "drag", "replace"];

    public static string OpponentRole(string playerRole)
    {
        if (playerRole == null)
        {
            return null;
        }

        return playerRole == "p1" ? "p2" : "p1";
    }

    /// <summary>
    /// The lines between the last two |turn| lines (none before turn 2).
    /// </summary>
    public static List<string[]> PreviousTurn(IReadOnlyList<string[]> lines)
    {
        var turns = Enumerable.Range(0, lines.Count)
            .Where(index => lines[index].Length > 1 && lines[index][1] == "turn")
            .ToList();
        return turns.Count < 2 ? [] : lines.Skip(turns[^2] + 1).Take(turns[^1] - turns[^2] - 1).ToList();
    }

    /// <summary>
    /// <paramref name="side"/> switched instead of moving: its |switch| comes before any |move| or |faint| of that
    /// side in the turn (a U-turn switch or a replacement after a faint comes after one of those).
    /// </summary>
    public static bool SwitchedByChoice(IEnumerable<string[]> turnLines, string side)
    {
        foreach (var line in turnLines.Where(line => line.Length >= 3 && SideOf(line[2]) == side))
        {
            if (line[1] is "move" or "faint")
            {
                return false;
            }

            if (line[1] == "switch")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The side's active Pokémon chose the same move the last two times since it came in (Struggle excluded).
    /// </summary>
    public static bool SameMoveTwice(IReadOnlyList<string[]> lines, string side)
    {
        var moves = LastChosenMoves(lines, side, 2);
        return moves.Count == 2 && moves[0] == moves[1];
    }

    /// <summary>
    /// The last <paramref name="count"/> moves the side's active Pokémon chose since it came in, newest first.
    /// </summary>
    public static List<string> LastChosenMoves(IReadOnlyList<string[]> lines, string side, int count)
    {
        var moves = new List<string>();
        foreach (var line in lines.Reverse().Where(line => line.Length >= 3 && SideOf(line[2]) == side))
        {
            if (ENTRANCES.Contains(line[1]))
            {
                break;
            }

            if (IsChosenMove(line) && line[3] != STRUGGLE)
            {
                moves.Add(line[3]);
                if (moves.Count == count)
                {
                    break;
                }
            }
        }

        return moves;
    }

    private static bool IsChosenMove(string[] line) =>
        line.Length > 3 && line[1] == "move" &&
        !line.Skip(4).Any(part => part.StartsWith(FROM_TAG, StringComparison.Ordinal));

    /// <summary>
    /// "p2a: Venusaur" -> "p2".
    /// </summary>
    private static string SideOf(string ident) => ident.Length > 2 ? ident[..2] : ident;
}
