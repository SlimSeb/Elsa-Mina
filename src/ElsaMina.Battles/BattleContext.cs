namespace ElsaMina.Battles;

public class BattleContext
{
    public BattleContext(string roomId)
    {
        RoomId = roomId;
    }

    public string RoomId { get; }
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
    public List<BattlePokemonState> SidePokemon { get; set; } = [];
    public List<BattleActiveSlot> ActiveSlots { get; set; } = [];
    public List<OpponentPokemonState> OpponentPokemon { get; set; } = [];
    public OpponentPokemonState ActiveOpponent => OpponentPokemon.FirstOrDefault(p => p.IsActive);
}
