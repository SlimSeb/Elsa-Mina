using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Tracking;

namespace ElsaMina.Battles;

public class BattleContext
{
    public BattleContext(string roomId)
    {
        RoomId = roomId;
    }

    public string RoomId { get; }

    /// <summary>
    /// Battle format extracted from the room id, e.g. "battle-gen9ou-12345" gives "gen9ou".
    /// </summary>
    public string Format
    {
        get
        {
            var segments = RoomId.Split('-');
            return segments.Length >= 2 ? segments[1] : "";
        }
    }

    public int Rqid { get; set; }
    public string SideId { get; set; } = "";
    public string OpponentSideId => SideId == "p1" ? "p2" : "p1";
    public bool Wait { get; set; }
    public bool TeamPreview { get; set; }
    public bool IsBattleOver { get; set; }
    public bool HasAnnouncedStart { get; set; }
    public bool HasAnnouncedEnd { get; set; }
    public List<bool> ForceSwitchSlots { get; set; } = [];

    /// <summary>
    /// Entry hazards set on our own side of the field, which damage our pokemon as they switch in.
    /// </summary>
    public bool OwnSideStealthRock { get; set; }
    public int OwnSideSpikesLayers { get; set; }

    /// <summary>
    /// Entry hazards we have set on the opponent's side. Tracked so the decision service knows what
    /// is already up and values setting the remaining hazards (rather than pointlessly re-setting).
    /// </summary>
    public bool OpponentSideStealthRock { get; set; }
    public int OpponentSideSpikesLayers { get; set; }
    public bool OpponentSideToxicSpikes { get; set; }
    public bool OpponentSideStickyWeb { get; set; }

    /// <summary>
    /// Whether the opponent's active pokemon is currently taunted (by us). Tracked so the decision
    /// service never wastes a turn re-taunting a target that is already locked out of status moves.
    /// Cleared when the opponent switches, since Taunt only applies to the pokemon that was on the field.
    /// </summary>
    public bool OpponentActiveTaunted { get; set; }

    public List<BattlePokemonState> SidePokemon { get; set; } = [];
    public List<BattleActiveSlot> ActiveSlots { get; set; } = [];
    public List<OpponentPokemonState> OpponentPokemon { get; set; } = [];
    public OpponentPokemonState ActiveOpponent => OpponentPokemon.FirstOrDefault(p => p.IsActive);

    /// <summary>
    /// The battle as poke-env tracks it, for the minipoke strategies (see <see cref="TrackedBattleState"/>).
    /// </summary>
    public TrackedBattleState Tracked { get; } = new();

    /// <summary>
    /// Every line received in the battle room, as received: what -savereplays keeps of our side, |request| lines
    /// included.
    /// </summary>
    public List<string> Lines { get; } = [];

    /// <summary>
    /// The strategy playing when the battle started, which its result is counted for.
    /// </summary>
    public BattleDecisionStrategy? Strategy { get; set; }
}
