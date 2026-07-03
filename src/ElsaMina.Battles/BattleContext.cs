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
    public string SideName { get; set; } = "";
    public string SideId { get; set; } = "";
    public string OpponentSideId => SideId == "p1" ? "p2" : "p1";
    public bool Wait { get; set; }
    public bool TeamPreview { get; set; }
    public bool NoCancel { get; set; }
    public bool IsBattleOver { get; set; }
    public bool HasAnnouncedStart { get; set; }
    public bool HasAnnouncedEnd { get; set; }
    public List<bool> ForceSwitchSlots { get; set; } = [];

    /// <summary>
    /// Entry hazards set on our own side of the field, which damage our pokemon as they switch in.
    /// Opponent-side hazards are not tracked because the simulation never switches the opponent out.
    /// </summary>
    public bool OwnSideStealthRock { get; set; }
    public int OwnSideSpikesLayers { get; set; }

    public List<BattlePokemonState> SidePokemon { get; set; } = [];
    public List<BattleActiveSlot> ActiveSlots { get; set; } = [];
    public List<OpponentPokemonState> OpponentPokemon { get; set; } = [];
    public OpponentPokemonState ActiveOpponent => OpponentPokemon.FirstOrDefault(p => p.IsActive);
}
