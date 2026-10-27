using ElsaMina.Battles.Tracking;

namespace ElsaMina.Battles.Strategies.MiniPokeV2;

/// <summary>
/// history.py's move_outcomes: what came of every chosen move in the protocol log. A |-fail| line after one marks it
/// failed, an |-immune| line on the other side records that the move had no effect on that Pokémon, until the next
/// |move|, |switch|, |drag|, |replace|, |turn|, |cant| or |upkeep| line. Pokémon are (side, name), as in "p2a: Name".
/// </summary>
public sealed class MoveOutcomes
{
    private const string STRUGGLE = "struggle";
    private static readonly HashSet<string> OUTCOME_END = ["move", "switch", "drag", "replace", "turn", "cant", "upkeep"];

    // (side, name, move id) -> whether each chosen use failed, oldest first
    private readonly Dictionary<(string Side, string Name, string Move), List<bool>> _uses = [];

    // (user side, move id, target side, target name) -> index of its last |-immune| line
    private readonly Dictionary<(string Side, string Move, string TargetSide, string TargetName), int> _immune = [];

    // (side, name) -> index of its last |-terastallize| line
    private readonly Dictionary<(string Side, string Name), int> _tera = [];

    public static MoveOutcomes Read(IReadOnlyList<string[]> lines)
    {
        var outcomes = new MoveOutcomes();
        (string Side, string Name, string Move)? pending = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length < 2)
            {
                continue;
            }

            var kind = line[1];
            if (OUTCOME_END.Contains(kind))
            {
                pending = null;
            }

            if (kind == "move" && ProtocolHistory.IsChosenMove(line))
            {
                var (side, name) = ProtocolHistory.SideAndName(line[2]);
                var move = ShowdownId.ToId(line[3]);
                if (move != STRUGGLE)
                {
                    pending = (side, name, move);
                    if (!outcomes._uses.TryGetValue(pending.Value, out var uses))
                    {
                        uses = [];
                        outcomes._uses[pending.Value] = uses;
                    }

                    uses.Add(false);
                }
            }
            else if (kind == "-fail" && pending != null)
            {
                outcomes._uses[pending.Value][^1] = true;
            }
            else if (kind == "-immune" && pending != null && line.Length > 2)
            {
                var (targetSide, targetName) = ProtocolHistory.SideAndName(line[2]);
                if (targetSide != pending.Value.Side)
                {
                    outcomes._immune[(pending.Value.Side, pending.Value.Move, targetSide, targetName)] = i;
                }
            }
            else if (kind == "-terastallize" && line.Length > 2)
            {
                outcomes._tera[ProtocolHistory.SideAndName(line[2])] = i;
            }
        }

        return outcomes;
    }

    /// <summary>
    /// The last <paramref name="count"/> uses of the move by that Pokémon all failed.
    /// </summary>
    public bool FailedLast(string side, string name, string move, int count) =>
        _uses.TryGetValue((side, name, move), out var uses) && uses.Count >= count &&
        uses.Skip(uses.Count - count).All(failed => failed);

    /// <summary>
    /// The side's move had no effect on that Pokémon since it last terastallized.
    /// </summary>
    public bool ImmuneBefore(string side, string move, string targetSide, string targetName) =>
        _immune.TryGetValue((side, move, targetSide, targetName), out var line) &&
        line > _tera.GetValueOrDefault((targetSide, targetName), -1);
}
