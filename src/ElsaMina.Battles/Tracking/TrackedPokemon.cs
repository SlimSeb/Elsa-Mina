using ElsaMina.Battles.Dtos;

namespace ElsaMina.Battles.Tracking;

/// <summary>
/// A Pokémon tracked exactly like poke-env 0.16.1's Pokemon, the state minipoke was trained on. Method names
/// follow poke-env's so the two can be compared line by line; this only keeps what the encoder reads and what
/// changes it.
/// </summary>
public sealed class TrackedPokemon
{
    public const string UNKNOWN_ITEM = "unknown_item";
    private const int MAX_BOOST = 6;
    private const string ILLUSION = "illusion";
    private const string REGENERATOR = "regenerator";
    private const string ABILITY_SHIELD = "abilityshield";
    private const string POWER_HERB = "powerherb";
    private const string MEGA_FORME_PREFIX = "Mega";
    private const string TERA_FORME_SUFFIX = "-Tera";
    private static readonly string[] COMMANDER_LIKE_MOVES = ["copycat", "metronome", "mefirst", "mirrormove", "assist"];

    private readonly IBattleDex _dex;
    private string _formeChangeAbility;
    private string _temporaryAbility;
    private string _type1;
    private string _type2;
    private IReadOnlyDictionary<string, int> _baseStats = new Dictionary<string, int>();
    private IReadOnlyDictionary<string, int> _temporaryBaseStats;
    private List<string> _temporaryTypes = [];
    private TrackedMove _preparingMove;
    private bool _hasPreparingTarget;
    private string _lastDetails = "";

    public TrackedPokemon(IBattleDex dex, string identifier)
    {
        _dex = dex;
        Identifier = identifier;
        Boosts = PokeEnvNames.Boosts.ToDictionary(stat => stat, _ => 0);
    }

    /// <summary>
    /// The key poke-env stores this Pokémon under in battle.team: "p1: Garchomp".
    /// </summary>
    public string Identifier { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Species id from the details ("ogerponwellspring"). Forme changes during a battle keep it until the
    /// Pokémon's details change (next switch-in, or next request for ours).
    /// </summary>
    public string Species { get; private set; } = "";

    public int Level { get; private set; } = 100;
    public IReadOnlyList<string> PossibleAbilities { get; private set; } = [];
    public bool Active { get; set; }
    public Dictionary<string, int> Boosts { get; private set; }

    /// <summary>
    /// Current HP: exact for ours, out of 100 for the opponent's.
    /// </summary>
    public int CurrentHp { get; private set; }

    public int MaxHp { get; private set; }

    /// <summary>
    /// poke-env status name ("BRN", "FNT"...), or null.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Volatile effects as poke-env Effect names ("SUBSTITUTE", "LEECH_SEED", "PERISH1"...).
    /// </summary>
    public HashSet<string> Effects { get; } = [];

    /// <summary>
    /// End of turns seen since this Pokémon switched in.
    /// </summary>
    public int ActiveTurns { get; private set; }

    public bool Terastallized { get; private set; }

    /// <summary>
    /// Tera type, known only once the Pokémon has terastallized (poke-env never reads it from the request).
    /// </summary>
    public string TeraType { get; private set; }

    /// <summary>
    /// Item id, null when it has none, <see cref="UNKNOWN_ITEM"/> when not revealed yet.
    /// </summary>
    public string Item { get; set; } = UNKNOWN_ITEM;

    /// <summary>
    /// poke-env's _ability: the ability before any temporary change or forme change.
    /// </summary>
    public string InnateAbility { get; set; }

    public bool MustRecharge { get; set; }
    public bool Dancing { get; set; }
    public TrackedMoveSet MoveSet { get; } = new();
    public SidePokemon LastRequest { get; private set; }

    public string TemporaryAbility
    {
        get => _temporaryAbility;
        set => _temporaryAbility = ToIdOrNull(value);
    }

    /// <summary>
    /// The current ability id, or null when unknown.
    /// </summary>
    public string Ability => _temporaryAbility ?? _formeChangeAbility ?? InnateAbility;

    public bool HasIllusionAbility => InnateAbility == ILLUSION;

    public bool Fainted => Status == PokeEnvNames.FAINTED;
    public bool FirstTurn => ActiveTurns == 1;
    public bool Preparing => _hasPreparingTarget || _preparingMove != null;
    public bool Transformed => MoveSet.TransformMoves != null;
    public OrderedDictionary<string, TrackedMove> Moves => MoveSet.Moves;
    public IReadOnlyDictionary<string, int> BaseStats => _temporaryBaseStats ?? _baseStats;
    public double CurrentHpFraction => CurrentHp != 0 ? (double)CurrentHp / MaxHp : 0;

    public string Type1
    {
        get
        {
            if (Terastallized && TeraType != null)
            {
                return TeraType;
            }

            return _temporaryTypes.Count > 0 ? _temporaryTypes[0] : _type1;
        }
    }

    public string Type2
    {
        get
        {
            if (Terastallized || _temporaryTypes.Count == 1)
            {
                return null;
            }

            return _temporaryTypes.Count > 1 ? _temporaryTypes[1] : _type2;
        }
    }

    public IReadOnlyList<string> Types => Type2 == null ? [Type1] : [Type1, Type2];

    public string BaseSpecies => _dex.TryGetSpecies(Species, out var entry)
        ? entry.BaseSpecies
        : throw new BattleTrackingException($"Unknown species {Species}");

    /// <summary>
    /// The ability setter: reveals the base ability first, later reveals are temporary changes.
    /// </summary>
    public void SetAbility(string ability)
    {
        if (InnateAbility == null)
        {
            InnateAbility = ShowdownId.ToId(ability);
        }
        else
        {
            TemporaryAbility = ability;
        }
    }

    /// <summary>
    /// The item setter, which normalizes the name.
    /// </summary>
    public void SetItem(string item)
    {
        Item = ToIdOrNull(item);
    }

    private static string ToIdOrNull(string name) => name == null ? null : ShowdownId.ToId(name);

    public void InitializeFromRequest(SidePokemon request, string name)
    {
        UpdateFromRequest(request);
        Name = name;
    }

    public void InitializeFromDetails(string details, string name)
    {
        UpdateFromDetails(details);
        Name = name;
    }

    public void InitializeFromSpecies(string species, string name)
    {
        UpdateFromPokedex(species);
        Name = name;
    }

    public TrackedMove AddMove(string moveId)
    {
        var id = ShowdownId.ToMoveId(moveId);
        if (Moves.TryGetValue(id, out var existing))
        {
            return existing;
        }

        if (!_dex.ShouldStoreMove(id))
        {
            return null;
        }

        var baseMoves = MoveSet.BaseMoves;
        if (!baseMoves.TryGetValue(id, out var move))
        {
            move = _dex.CreateMove(id, fromTransform: Transformed);
            baseMoves[id] = move;
        }

        return move;
    }

    public void Boost(string stat, int amount)
    {
        if (!Boosts.TryGetValue(stat, out var current))
        {
            throw new BattleTrackingException($"Unknown boost {stat}");
        }

        Boosts[stat] = Math.Clamp(current + amount, -MAX_BOOST, MAX_BOOST);
    }

    public void SetBoost(string stat, int amount)
    {
        if (Math.Abs(amount) > MAX_BOOST)
        {
            throw new BattleTrackingException($"Boost {stat} of {Species} is not <= 6: {amount}");
        }

        Boosts[stat] = amount;
    }

    public void CantMove()
    {
        Dancing = false;
    }

    public void ClearBoosts()
    {
        foreach (var stat in Boosts.Keys.ToList())
        {
            Boosts[stat] = 0;
        }
    }

    public void ClearNegativeBoosts()
    {
        foreach (var stat in Boosts.Where(pair => pair.Value < 0).Select(pair => pair.Key).ToList())
        {
            Boosts[stat] = 0;
        }
    }

    public void ClearPositiveBoosts()
    {
        foreach (var stat in Boosts.Where(pair => pair.Value > 0).Select(pair => pair.Key).ToList())
        {
            Boosts[stat] = 0;
        }
    }

    public void CopyBoosts(TrackedPokemon other)
    {
        Boosts = new Dictionary<string, int>(other.Boosts);
    }

    public void InvertBoosts()
    {
        Boosts = Boosts.ToDictionary(pair => pair.Key, pair => -pair.Value);
    }

    public void CureStatus(string status = null)
    {
        // A named status is only cured if it is the current one; no name cures any status but fainting
        var curesCurrentStatus = string.IsNullOrEmpty(status)
            ? status == null && !Fainted
            : PokeEnvNames.StatusFromName(status) == Status;
        if (curesCurrentStatus)
        {
            Status = null;
        }
    }

    public void StartEffect(string effectMessage, string typeChangeDetails = null,
        IReadOnlyList<string> skillSwapAbilities = null)
    {
        var effect = PokeEnvNames.EffectFromMessage(effectMessage);
        Effects.Add(effect);

        if (effect == "TYPECHANGE" && typeChangeDetails != null)
        {
            _temporaryTypes = typeChangeDetails.Split('/').Select(PokeEnvNames.TypeFromName).ToList();
        }
        else if (effect == "SKILL_SWAP" && skillSwapAbilities is { Count: > 0 })
        {
            if (Ability == null)
            {
                SetAbility(skillSwapAbilities[1]);
            }

            TemporaryAbility = skillSwapAbilities[0];
        }
    }

    public void EndEffect(string effectMessage)
    {
        var effect = PokeEnvNames.EffectFromMessage(effectMessage);
        Effects.Remove(effect);

        switch (effect)
        {
            case "TYPECHANGE":
                _temporaryTypes = [];
                break;
            case "SKILL_SWAP":
                TemporaryAbility = null;
                break;
            case "QUARK_DRIVE":
                Effects.ExceptWith(PokeEnvNames.QuarkDriveEffects);
                break;
            case "PROTOSYNTHESIS":
                Effects.ExceptWith(PokeEnvNames.ProtosynthesisEffects);
                break;
        }
    }

    private void ClearEffects()
    {
        foreach (var effect in Effects.ToList())
        {
            EndEffect(effect);
        }
    }

    public void EndItem(string item)
    {
        Item = null;
        // poke-env compares the raw name, so "Power Herb" never matches
        if (item == POWER_HERB)
        {
            _preparingMove = null;
            _hasPreparingTarget = false;
        }
    }

    public void EndTurn()
    {
        ActiveTurns++;
        foreach (var effect in Effects.Where(PokeEnvNames.EffectsEndingOnTurn.Contains).ToList())
        {
            EndEffect(effect);
        }
    }

    public void Faint()
    {
        CurrentHp = 0;
        Status = PokeEnvNames.FAINTED;
        TemporaryAbility = null;
        _temporaryBaseStats = null;
        MoveSet.TransformMoves = null;
        MoveSet.MimicMove = null;
        ClearEffects();
    }

    public void FormeChange(string species)
    {
        UpdateFromPokedex(species.Split(',')[0], storeSpecies: false);
    }

    public bool IdentifiesAs(string ident)
    {
        var baseSpecies = BaseSpecies;
        return baseSpecies == ShowdownId.ToId(ident) || ident.Split('-').Any(part => ShowdownId.ToId(part) == baseSpecies);
    }

    /// <summary>
    /// The Pokémon used a move. poke-env's failed flag only feeds the protect counter, which no feature reads.
    /// </summary>
    public void Moved(string moveId, bool use = true, bool reveal = true, bool pressure = false)
    {
        MustRecharge = false;
        _preparingMove = null;
        _hasPreparingTarget = false;
        var move = reveal ? AddMove(moveId) : null;
        if (use)
        {
            move?.Use(pressure);
        }

        // Effects that silently end when the Pokémon moves
        if (Effects.Contains("GLAIVE_RUSH"))
        {
            EndEffect("Glaive Rush");
        }
        else if (Effects.Contains("CHARGE") && use && IsDamagingMoveOfType(move, "ELECTRIC"))
        {
            EndEffect("Charge");
        }
        else if (Effects.Contains("FLASH_FIRE") && use && IsDamagingMoveOfType(move, "FIRE"))
        {
            EndEffect("Flash Fire");
        }
    }

    private bool IsDamagingMoveOfType(TrackedMove move, string type)
    {
        return move != null
               && _dex.TryGetMoveEntry(move.Id, out var entry)
               && entry.BasePower > 0
               && PokeEnvNames.TypeFromName(entry.Type) == type;
    }

    public void Prepare(string moveId, bool hasTarget)
    {
        var id = ShowdownId.ToMoveId(moveId);
        _preparingMove = Moves.TryGetValue(id, out var known) ? known : _dex.CreateMove(id);
        _hasPreparingTarget = hasTarget;
    }

    /// <summary>
    /// set_hp_status: "0 fnt", "123/300", or "123/300 par". An HP string without a status clears the status.
    /// </summary>
    public void SetHpStatus(string hpStatus)
    {
        if (hpStatus == "0 fnt")
        {
            CurrentHp = 0;
            Status = PokeEnvNames.FAINTED;
            return;
        }

        string hp;
        if (hpStatus.Contains(' '))
        {
            var parts = hpStatus.Split(' ');
            if (parts.Length != 2)
            {
                throw new BattleTrackingException($"Unexpected HP status '{hpStatus}'");
            }

            hp = parts[0];
            Status = PokeEnvNames.StatusFromName(parts[1]);
            if (Status == PokeEnvNames.SLEEP && Effects.Contains("YAWN"))
            {
                EndEffect("yawn");
            }
        }
        else
        {
            hp = hpStatus;
            Status = null;
        }

        var values = string.Concat(hp.Where(character => character is >= '0' and <= '9' or '/')).Split('/');
        if (values.Length != 2 || !int.TryParse(values[0], out var currentHp) ||
            !int.TryParse(values[1], out var maxHp))
        {
            throw new BattleTrackingException($"Unexpected HP '{hp}'");
        }

        CurrentHp = currentHp;
        MaxHp = maxHp;
    }

    public void SwitchIn(string details = null)
    {
        Active = true;
        if (!string.IsNullOrEmpty(details))
        {
            UpdateFromDetails(details);
        }
    }

    public void SwitchOut(bool neutralizingGasActive)
    {
        if (Ability == REGENERATOR && (Item == ABILITY_SHIELD || !neutralizingGasActive) && !Fainted)
        {
            CurrentHp = Math.Min((int)(CurrentHp + MaxHp / 3.0), MaxHp);
        }

        Active = false;
        ClearBoosts();
        ClearEffects();
        ActiveTurns = 0;
        MustRecharge = false;
        _preparingMove = null;
        _hasPreparingTarget = false;
        TemporaryAbility = null;
        _temporaryBaseStats = null;
        MoveSet.TransformMoves = null;
        MoveSet.MimicMove = null;
        _temporaryTypes = [];
    }

    public void Terastallize(string type)
    {
        TeraType = PokeEnvNames.TypeFromName(type);
        Terastallized = true;
        _temporaryTypes = [];
    }

    public void Transform(TrackedPokemon into)
    {
        if (!_dex.TryGetSpecies(into.Species, out var entry))
        {
            throw new BattleTrackingException($"Unknown species {into.Species}");
        }

        _temporaryBaseStats = entry.BaseStats;
        if (into.Ability != null)
        {
            TemporaryAbility = into.Ability;
        }

        _temporaryTypes = entry.Types.Select(PokeEnvNames.TypeFromName).ToList();
        var copiedMoves = new TrackedMoveSet();
        foreach (var moveId in into.Moves.Values.Select(move => move.Id))
        {
            copiedMoves.BaseMoves[moveId] = _dex.CreateMove(moveId, fromTransform: true);
        }

        MoveSet.TransformMoves = copiedMoves;
        Boosts = new Dictionary<string, int>(into.Boosts);
    }

    public void UpdateFromPokedex(string species, bool storeSpecies = true)
    {
        var id = ShowdownId.ToId(species);
        if (!_dex.TryGetSpecies(id, out var entry))
        {
            throw new BattleTrackingException($"Unknown species {id}");
        }

        if (storeSpecies)
        {
            Species = id;
        }

        _baseStats = entry.BaseStats;
        _type1 = PokeEnvNames.TypeFromName(entry.Types[0]);
        _type2 = entry.Types.Count == 1 ? null : PokeEnvNames.TypeFromName(entry.Types[1]);

        if (IsFormeWithFixedAbility(entry.Forme))
        {
            _formeChangeAbility = entry.Abilities.Count > 0
                ? ShowdownId.ToId(entry.Abilities[0])
                : throw new BattleTrackingException($"No ability for {id}");
        }
        else if (_formeChangeAbility == null)
        {
            PossibleAbilities = entry.Abilities.Select(ShowdownId.ToId).ToList();
            if (PossibleAbilities.Count == 1)
            {
                InnateAbility = PossibleAbilities[0];
            }
        }
        else
        {
            _formeChangeAbility = null;
        }
    }

    private static bool IsFormeWithFixedAbility(string forme)
    {
        return forme != null && (forme.StartsWith(MEGA_FORME_PREFIX, StringComparison.Ordinal)
                                 || forme is "Primal" or "Stellar" or "Terastal"
                                 || forme.EndsWith(TERA_FORME_SUFFIX, StringComparison.Ordinal));
    }

    /// <summary>
    /// _update_from_details: "Garchomp, L80, M, shiny, tera:Fire".
    /// </summary>
    public void UpdateFromDetails(string details)
    {
        if (details == _lastDetails)
        {
            return;
        }

        _lastDetails = details;
        details = details.Replace(", shiny", "");

        var parts = details.Split(", ").ToList();
        var teraPart = parts.FirstOrDefault(part => part.StartsWith("tera:", StringComparison.Ordinal));
        if (teraPart != null)
        {
            TeraType = PokeEnvNames.TypeFromName(teraPart[5..]);
            parts.Remove(teraPart);
        }

        string species;
        string level = null;
        if (parts.Count == 3 || (parts.Count == 2 && parts[1].StartsWith('L')))
        {
            species = parts[0];
            level = parts[1];
        }
        else if (parts.Count == 2)
        {
            species = parts[0];
        }
        else
        {
            species = ShowdownId.ToId(parts[0]);
        }

        if (level == null)
        {
            Level = 100;
        }
        else if (int.TryParse(level[1..], out var parsedLevel))
        {
            Level = parsedLevel;
        }
        else
        {
            throw new BattleTrackingException($"Unexpected level in '{details}'");
        }

        if (species != Species)
        {
            UpdateFromPokedex(species);
        }
    }

    public void UpdateFromRequest(SidePokemon request)
    {
        Active = request.Active;
        if (Ability == null)
        {
            SetAbility(request.BaseAbility);
        }

        if (request.Ability != null && request.Ability != request.BaseAbility)
        {
            TemporaryAbility = request.Ability;
        }

        LastRequest = request;
        SetHpStatus(request.Condition);
        Name = request.Ident[4..];
        Item = request.Item;
        UpdateFromDetails(request.Details);
        foreach (var move in request.Moves)
        {
            AddMove(move);
        }
    }

    /// <summary>
    /// What poke-env does to the Pokémon an Illusion was disguised as, once the Illusion ends.
    /// </summary>
    public void WasIllusioned(bool neutralizingGasActive)
    {
        CurrentHp = 0;
        MaxHp = 0;
        Status = null;
        var lastRequest = LastRequest;
        LastRequest = null;
        if (lastRequest != null)
        {
            UpdateFromRequest(lastRequest);
        }

        SwitchOut(neutralizingGasActive);
    }

    /// <summary>
    /// The moves of the active request this Pokémon can use, as known moves when possible.
    /// </summary>
    public List<TrackedMove> AvailableMovesFromRequest(ActivePokemon request)
    {
        if (Effects.Contains("COMMANDER"))
        {
            return [];
        }

        var moves = Moves;
        var available = new List<TrackedMove>();
        foreach (var id in request.Moves.Where(move => !move.Disabled).Select(move => move.Id))
        {
            if (moves.TryGetValue(id, out var known))
            {
                available.Add(known);
            }
            else if (PokeEnvNames.SpecialMoves.Contains(id))
            {
                available.Add(_dex.CreateMove(id));
            }
            else if (id == "hiddenpower" && moves.Keys.Count(key => key.StartsWith("hiddenpower", StringComparison.Ordinal)) == 1)
            {
                available.Add(moves.First(pair => pair.Key.StartsWith("hiddenpower", StringComparison.Ordinal)).Value);
            }
            else if (Ability == "dancer" || COMMANDER_LIKE_MOVES.Any(moves.ContainsKey))
            {
                available.Add(_dex.CreateMove(id));
            }
            else
            {
                throw new BattleTrackingException($"Unknown request move {id} for {Species}");
            }
        }

        return available;
    }
}
