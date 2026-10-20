namespace ElsaMina.Battles.Tracking;

/// <summary>
/// The battle as poke-env 0.16.1 sees it (poke_env.battle.Battle), which is what minipoke was trained on. Kept
/// next to the rest of <see cref="BattleContext"/>, which the other strategies read and which tracks some of the
/// same things differently.
/// </summary>
public class TrackedBattleState
{
    /// <summary>
    /// Our side ("p1" or "p2"), known from the first request.
    /// </summary>
    public string PlayerRole { get; set; }

    /// <summary>
    /// Our Pokémon in the order they were first seen (poke-env's battle.team). Stays stable while Showdown
    /// reorders the request's side after every switch.
    /// </summary>
    public List<TrackedPokemon> Team { get; } = [];

    /// <summary>
    /// The opponent's revealed Pokémon in the order they were first seen (battle.opponent_team).
    /// </summary>
    public List<TrackedPokemon> OpponentTeam { get; } = [];

    public Dictionary<string, int> TeamSizes { get; } = [];
    public int Turn { get; set; }

    /// <summary>
    /// The current weather, mapped to the turn it started (at most one entry).
    /// </summary>
    public Dictionary<string, int> Weather { get; set; } = [];

    /// <summary>
    /// Field effects (terrains, Trick Room, Gravity...), mapped to the turn they started.
    /// </summary>
    public Dictionary<string, int> Fields { get; } = [];

    /// <summary>
    /// Side conditions on our side, mapped to their number of layers (Spikes, Toxic Spikes) or the turn they
    /// started (all others).
    /// </summary>
    public Dictionary<string, int> SideConditions { get; set; } = [];

    public Dictionary<string, int> OpponentSideConditions { get; set; } = [];
    public bool UsedTera { get; set; }
    public bool OpponentUsedTera { get; set; }

    /// <summary>
    /// The active Pokémon's usable moves in the last request, as known moves when possible.
    /// </summary>
    public List<TrackedMove> AvailableMoves { get; set; } = [];

    public List<TrackedPokemon> AvailableSwitches { get; set; } = [];
    public bool CanTera { get; set; }
    public bool ForceSwitch { get; set; }
    public bool Trapped { get; set; }
    public bool Reviving { get; set; }
    public bool Wait { get; set; }

    /// <summary>
    /// False when the last request could not be applied (poke-env would have raised and not chosen a move).
    /// </summary>
    public bool IsRequestTracked { get; set; }

    /// <summary>
    /// Lines received before the first request told us our side, replayed once it is known.
    /// </summary>
    public List<string[]> PendingLines { get; } = [];

    public TrackedPokemon ActivePokemon => Team.FirstOrDefault(pokemon => pokemon.Active);
    public TrackedPokemon OpponentActivePokemon => OpponentTeam.FirstOrDefault(pokemon => pokemon.Active);
    public bool IsNeutralizingGasActive => Fields.ContainsKey(PokeEnvNames.NEUTRALIZING_GAS);

    public List<TrackedPokemon> GetTeam(string role) => role == PlayerRole ? Team : OpponentTeam;

    public static TrackedPokemon Find(List<TrackedPokemon> team, string identifier) =>
        team.FirstOrDefault(pokemon => pokemon.Identifier == identifier);
}
