using ElsaMina.Battles.Strategies.MiniPokeV2;
using ElsaMina.Battles.Tracking;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// Vetoes the actions a minipoke model would waste for sure, read from the battle's lines and the current request
/// (tools/minipoke/analyze_replays.py's guard_vetoes applies the same rules to saved battles):
/// <list type="bullet">
/// <item>a move our active Pokémon already used on the opposing active with no effect (|-immune|), while neither
/// of them terastallized since (a few moves whose type or immunity changes during a battle excepted);</item>
/// <item>a move that only heals the user, at full HP;</item>
/// <item>Sleep Talk and Snore while awake;</item>
/// <item>a hazard the opponent's side already has every layer of;</item>
/// <item>every switch, after Showdown said our active Pokémon is trapped (a trapping ability it doesn't announce),
/// until either side's active Pokémon changes.</item>
/// </list>
/// The decision service plays the best action left; a model that only has vetoed actions keeps its choice.
/// </summary>
public class MiniPokeActionGuard
{
    public const string TRAPPED_ERROR = "[Unavailable choice] Can't switch: The active Pokémon is trapped";
    private const int FIRST_MOVE_ACTION = 6;
    private const int FIRST_TERA_ACTION = 10;
    private const string REST = "rest";
    private const string ASLEEP = "slp";
    private const string COMATOSE = "comatose";
    private const string SELF_TARGET = "self";
    private const string MOVE_PREFIX = "move: ";

    // Their type, or what they hit, can change during a battle
    private static readonly HashSet<string> CHANGING_MOVES =
    [
        "terablast", "terastarstorm", "weatherball", "terrainpulse", "revelationdance", "aurawheel", "endeavor",
        "dreameater", "synchronoise"
    ];

    private static readonly Dictionary<string, int> HAZARD_LAYERS = new()
    {
        ["stealthrock"] = 1, ["stickyweb"] = 1, ["spikes"] = 3, ["toxicspikes"] = 2
    };

    private static readonly HashSet<string> SLEEP_MOVES = ["sleeptalk", "snore"];
    private static readonly HashSet<string> ENTRANCES = ["switch", "drag", "replace"];
    private static readonly HashSet<string> OUTCOME_END = ["move", "switch", "drag", "replace", "turn", "cant", "upkeep"];

    private readonly IBattleDex _dex;

    public MiniPokeActionGuard(IBattleDex dex)
    {
        _dex = dex;
    }

    /// <summary>
    /// The vetoed actions (0-13) and why.
    /// </summary>
    public IReadOnlyDictionary<int, string> GetVetoes(BattleContext context, BattleObservation observation)
    {
        var ours = context.SideId;
        var lines = context.Lines.Select(line => line.Split('|')).ToList();
        var history = Read(lines, ours);
        var vetoes = new Dictionary<int, string>();
        var active = context.SidePokemon.FirstOrDefault(pokemon => pokemon.IsActive);
        var atFullHp = active is { MaxHp: > 0 } && active.CurrentHp == active.MaxHp;
        var asleep = active != null && (active.Condition.Split(' ').Contains(ASLEEP) ||
                                        ShowdownId.ToId(active.Ability) == COMATOSE);

        for (var j = 0; j < BattleObservation.MoveCount; j++)
        {
            var move = observation.MoveSlotIds[j];
            var reason = move == null ? null : MoveVeto(move, history, atFullHp, asleep);
            if (reason != null)
            {
                vetoes[FIRST_MOVE_ACTION + j] = reason;
                vetoes[FIRST_TERA_ACTION + j] = reason;
            }
        }

        var forceSwitch = context.ForceSwitchSlots.FirstOrDefault();
        if (!forceSwitch && history.TrappedErrorLine > history.LastEntranceLine)
        {
            for (var slot = 0; slot < BattleObservation.SlotCount; slot++)
            {
                vetoes[slot] = "trapped";
            }
        }

        return vetoes;
    }

    private string MoveVeto(string move, GuardHistory history, bool atFullHp, bool asleep)
    {
        if (history.OurActive != null && history.OpponentActive != null && !CHANGING_MOVES.Contains(move) &&
            history.ImmuneLines.TryGetValue((history.OurActive, move, history.OpponentActive), out var line) &&
            line > history.TeraLines.GetValueOrDefault((history.Ours, history.OurActive), -1) &&
            line > history.TeraLines.GetValueOrDefault((history.Opponent, history.OpponentActive), -1))
        {
            return $"no effect on {history.OpponentActive} before";
        }

        if (atFullHp && (move == REST ||
                         (_dex.TryGetMove(move, out var entry) && entry.Heal is { Length: 2 } &&
                          entry.Target == SELF_TARGET)))
        {
            return "heals at full HP";
        }

        if (!asleep && SLEEP_MOVES.Contains(move))
        {
            return "only works asleep";
        }

        if (HAZARD_LAYERS.TryGetValue(move, out var layers) &&
            history.OpponentHazards.GetValueOrDefault(move) >= layers)
        {
            return "hazard already set";
        }

        return null;
    }

    private static GuardHistory Read(List<string[]> lines, string ours)
    {
        var history = new GuardHistory(ours, ProtocolHistory.OpponentRole(ours));
        var hazards = new Dictionary<string, Dictionary<string, int>> { [ours] = [], [history.Opponent] = [] };
        (string User, string Move)? pending = null;
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

            var (side, name) = line.Length > 2 ? ProtocolHistory.SideAndName(line[2]) : ("", "");
            if (kind == "-swapsideconditions")
            {
                (hazards[ours], hazards[history.Opponent]) = (hazards[history.Opponent], hazards[ours]);
            }
            else if (line.Length < 3)
            {
                continue;
            }
            else if (ENTRANCES.Contains(kind))
            {
                history.SetActive(side, name);
                history.LastEntranceLine = i;
            }
            else if (kind == "move" && side == ours && ProtocolHistory.IsChosenMove(line))
            {
                pending = (name, ShowdownId.ToId(line[3]));
            }
            else if (kind == "-immune" && pending != null && side == history.Opponent)
            {
                history.ImmuneLines[(pending.Value.User, pending.Value.Move, name)] = i;
            }
            else if (kind == "-terastallize")
            {
                history.TeraLines[(side, name)] = i;
            }
            else if (kind is "-sidestart" or "-sideend" && line.Length > 3 && hazards.TryGetValue(side, out var sideHazards))
            {
                var condition = ShowdownId.ToId(line[3].Replace(MOVE_PREFIX, ""));
                sideHazards[condition] = kind == "-sidestart" ? sideHazards.GetValueOrDefault(condition) + 1 : 0;
            }
            else if (kind == "error" && line[2].StartsWith(TRAPPED_ERROR, StringComparison.Ordinal))
            {
                history.TrappedErrorLine = i;
            }
        }

        history.OpponentHazards = hazards[history.Opponent];
        return history;
    }
}
