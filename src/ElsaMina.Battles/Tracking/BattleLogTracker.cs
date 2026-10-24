using ElsaMina.Logging;

namespace ElsaMina.Battles.Tracking;

/// <summary>
/// Applies battle log lines to a <see cref="TrackedBattleState"/> exactly like poke-env 0.16.1's
/// AbstractBattle.parse_message, quirks included (it is what minipoke learned from). Lines poke-env ignores, or
/// that only matter to other formats (team preview, Mega Evolution, Z-Moves, Dynamax), are skipped. When poke-env
/// would raise, the rest of the line is skipped and what it already changed stays.
/// </summary>
public class BattleLogTracker
{
    private const string FROM_ITEM = "[from] item:";
    private const string FROM_ABILITY = "[from] ability:";
    private const string OF_PREFIX = "[of] ";
    private const string MOVE_PREFIX = "move: ";
    private const string NEUTRALIZING_GAS = "Neutralizing Gas";

    private static readonly HashSet<string> PRESSURE_TARGETS =
        ["all", "allAdjacent", "allAdjacentFoes", "any", "normal", "randomNormal", "scripted"];

    private static readonly HashSet<string> CALYREX_ABILITIES = ["As One", "Unnerve", "Chilling Neigh", "Grim Neigh"];
    private static readonly HashSet<string> MOVES_CALLING_OTHERS = ["Sleep Talk", "Copycat", "Metronome", "Nature Power"];
    private static readonly HashSet<string> NON_REVEALING_CALLERS = ["Copycat", "Metronome", "Nature Power", "Round"];
    private static readonly HashSet<string> PLEDGES = ["Grass Pledge", "Water Pledge", "Fire Pledge"];
    private static readonly string[] FAILED_MOVE_SUFFIXES = ["[miss]", "[still]", "[notarget]"];
    private static readonly HashSet<string> PROTOCOL_LOG_TYPES = ["turn", "move", "switch", "drag", "replace", "faint"];

    private readonly IBattleDex _dex;
    private readonly TrackedPokemonResolver _resolver;

    public BattleLogTracker(IBattleDex dex, TrackedPokemonResolver resolver)
    {
        _dex = dex;
        _resolver = resolver;
    }

    public void Apply(string[] parts, TrackedBattleState state)
    {
        // poke-env logs every line before parsing it, even one it then raises on
        if (parts.Length > 1 && PROTOCOL_LOG_TYPES.Contains(parts[1]))
        {
            state.ProtocolLines.Add([..parts]);
        }

        try
        {
            ApplyMessage([..parts], state);
        }
        catch (Exception exception)
        {
            // poke-env raises on the line and moves on: do the same, keeping what was already applied
            Log.Warning("Battle tracking skipped '{0}': {1}", string.Join('|', parts), exception.Message);
        }
    }

    private void ApplyMessage(List<string> message, TrackedBattleState state)
    {
        switch (message[1])
        {
            case "switch" or "drag":
                Switch(state, message[2], message[3], message[4]);
                break;
            case "-damage":
                _resolver.Get(state, message[2]).SetHpStatus(message[3]);
                CheckDamageMessageForItem(state, message);
                CheckDamageMessageForAbility(state, message);
                break;
            case "move":
                ApplyMove(state, message);
                break;
            case "cant":
                RequireLength(message, 4);
                _resolver.Get(state, message[2]).CantMove();
                break;
            case "turn":
                EndTurn(state, int.Parse(message[2]));
                break;
            case "-heal":
                _resolver.Get(state, message[2]).SetHpStatus(message[3]);
                CheckHealMessageForAbility(state, message);
                CheckHealMessageForItem(state, message);
                break;
            case "-boost":
                RequireLength(message, 5);
                _resolver.Get(state, message[2]).Boost(message[3], int.Parse(message[4]));
                break;
            case "-unboost":
                RequireLength(message, 5);
                _resolver.Get(state, message[2]).Boost(message[3], -int.Parse(message[4]));
                break;
            case "-weather":
                state.Weather = message[2] == "none"
                    ? []
                    : new Dictionary<string, int> { [PokeEnvNames.WeatherFromMessage(message[2])] = state.Turn };
                break;
            case "faint":
                _resolver.Get(state, message[2]).Faint();
                break;
            case "-ability":
                ApplyAbility(state, message);
                break;
            case "-start":
                ApplyStart(state, message);
                break;
            case "-activate":
                ApplyActivate(state, message);
                break;
            case "-status":
                RequireLength(message, 4);
                _resolver.Get(state, message[2]).Status = PokeEnvNames.StatusFromName(message[3]);
                break;
            case "-clearallboost":
                state.ActivePokemon?.ClearBoosts();
                state.OpponentActivePokemon?.ClearBoosts();
                break;
            case "-clearboost":
                _resolver.Get(state, message[2]).ClearBoosts();
                break;
            case "-clearnegativeboost":
                _resolver.Get(state, message[2]).ClearNegativeBoosts();
                break;
            case "-clearpositiveboost":
                _resolver.Get(state, message[2]).ClearPositiveBoosts();
                break;
            case "-copyboost":
                // poke-env copies onto the second Pokémon, the reverse of the protocol's meaning: keep it
                RequireLength(message, 4);
                _resolver.Get(state, message[3]).CopyBoosts(_resolver.Get(state, message[2]));
                break;
            case "-curestatus":
                RequireLength(message, 4);
                _resolver.Get(state, message[2]).CureStatus(message[3]);
                break;
            case "-cureteam":
                foreach (var pokemon in state.GetTeam(message[2][..2]))
                {
                    pokemon.CureStatus();
                }

                break;
            case "-end":
                RequireLength(message, 4);
                if (message[3] == "ability: Neutralizing Gas")
                {
                    FieldEnd(state, message[3]);
                }
                else
                {
                    _resolver.Get(state, message[2]).EndEffect(message[3]);
                }

                break;
            case "-endability":
                _resolver.Get(state, message[2]).TemporaryAbility = null;
                break;
            case "-enditem":
                RequireLength(message, 4);
                _resolver.Get(state, message[2]).EndItem(message[3]);
                break;
            case "-fieldend":
                FieldEnd(state, message[2]);
                break;
            case "-fieldstart":
                FieldStart(state, message[2]);
                break;
            case "-formechange" or "detailschange":
                RequireLength(message, 4);
                _resolver.Get(state, message[2]).FormeChange(message[3]);
                break;
            case "-invertboost":
                _resolver.Get(state, message[2]).InvertBoosts();
                break;
            case "-item":
                ApplyItem(state, message);
                break;
            case "-mustrecharge":
                _resolver.Get(state, message[2]).MustRecharge = true;
                break;
            case "-prepare":
                ApplyPrepare(state, message);
                break;
            case "-setboost":
                RequireLength(message, 5);
                _resolver.Get(state, message[2]).SetBoost(message[3], int.Parse(message[4]));
                break;
            case "-sethp":
                RequireLength(message, 4);
                _resolver.Get(state, message[2]).SetHpStatus(message[3]);
                break;
            case "-sideend":
                RequireLength(message, 4);
                SideEnd(state, message[2], message[3]);
                break;
            case "-sidestart":
                RequireLength(message, 4);
                SideStart(state, message[2], message[3]);
                break;
            case "-singleturn" or "-singlemove":
                RequireLength(message, 4);
                _resolver.Get(state, message[2]).StartEffect(message[3].Replace(MOVE_PREFIX, ""));
                break;
            case "-swapboost":
                ApplySwapBoost(state, message);
                break;
            case "-transform":
                ApplyTransform(state, message);
                break;
            case "replace":
                EndIllusion(state, message[2], message[3]);
                break;
            case "teamsize":
                RequireLength(message, 4);
                state.TeamSizes[message[2]] = int.Parse(message[3]);
                break;
            case "-immune":
                if (message.Count == 4 && message[3].StartsWith(FROM_ABILITY, StringComparison.Ordinal))
                {
                    _resolver.Get(state, message[2]).SetAbility(message[3].Replace(FROM_ABILITY, ""));
                }

                break;
            case "-swapsideconditions":
                (state.SideConditions, state.OpponentSideConditions) =
                    (state.OpponentSideConditions, state.SideConditions);
                break;
            case "-terastallize":
                ApplyTerastallize(state, message);
                break;
        }
    }

    /// <summary>
    /// Battle.switch: the previous active Pokémon of that side switches out, then the new one switches in.
    /// </summary>
    private void Switch(TrackedBattleState state, string pokemonIdent, string details, string hpStatus)
    {
        var previous = pokemonIdent.Split(':')[0][..2] == state.PlayerRole
            ? state.ActivePokemon
            : state.OpponentActivePokemon;
        previous?.SwitchOut(state.IsNeutralizingGasActive);

        var pokemon = _resolver.Get(state, pokemonIdent, details: details);
        pokemon.SwitchIn(details);
        pokemon.SetHpStatus(hpStatus);
    }

    private static void EndTurn(TrackedBattleState state, int turn)
    {
        state.Turn = turn;
        state.ActivePokemon?.EndTurn();
        state.OpponentActivePokemon?.EndTurn();
    }

    private void ApplyMove(TrackedBattleState state, List<string> message)
    {
        var mon = _resolver.Get(state, message[2]);
        var use = !mon.Dancing;
        var failed = false;
        var reveal = !mon.Dancing;
        string overriddenMove = null;
        var spread = false;
        mon.Dancing = false;

        foreach (var failedSuffix in FAILED_MOVE_SUFFIXES)
        {
            if (message[^1] == failedSuffix)
            {
                RemoveLast(message);
                failed = true;
            }
        }

        if (message[^1] == "[notarget]")
        {
            RemoveLast(message);
        }

        while (message[^1].StartsWith("[spread]", StringComparison.Ordinal))
        {
            spread = true;
            RemoveLast(message);
        }

        while (message[^1] == "[still]")
        {
            RemoveLast(message);
        }

        if (message[^1] is "[from] lockedmove" or "[from]lockedmove" or "[from] Sky Attack")
        {
            use = false;
            reveal = false;
            RemoveLast(message);
        }

        if (message[^1] is "[from] Pursuit" or "[from]Pursuit" or "[zeffect]")
        {
            RemoveLast(message);
        }

        if (message[^1] == "[from] Sleep Talk")
        {
            message[^1] = "[from] move: Sleep Talk";
        }

        if (message[^1].StartsWith("[anim]", StringComparison.Ordinal))
        {
            RemoveLast(message);
        }

        if (message[^1].StartsWith("[from] move: ", StringComparison.Ordinal) ||
            message[^1].StartsWith("[from]move: ", StringComparison.Ordinal))
        {
            overriddenMove = PopLast(message).Split(": ")[^1];
            if (NON_REVEALING_CALLERS.Contains(overriddenMove))
            {
                reveal = false;
            }
            else if (PLEDGES.Contains(overriddenMove))
            {
                overriddenMove = null;
            }
        }

        if (message[^1] == "null")
        {
            RemoveLast(message);
        }

        if (message[^1].StartsWith("[from] ability: ", StringComparison.Ordinal) ||
            message[^1].StartsWith("[from]ability: ", StringComparison.Ordinal))
        {
            var revealedAbility = PopLast(message).Split(": ")[^1];
            _resolver.Get(state, message[2]).SetAbility(revealedAbility);
            if (revealedAbility == "Magic Bounce")
            {
                use = false;
                reveal = false;
            }
            else if (revealedAbility == "Dancer")
            {
                return;
            }
        }

        if (message[^1] is "[from] Magic Coat" or "[from] Mirror Move")
        {
            use = false;
            reveal = false;
            RemoveLast(message);
        }

        RequireLength(message, 4);
        var pokemon = message[2];
        var move = message[3];
        var presumedTarget = message.Count >= 5 ? message[4] : null;

        if (move.ToUpperInvariant().Trim() == "MINIMIZE")
        {
            _resolver.Get(state, pokemon).StartEffect("MINIMIZE");
        }

        if (spread || presumedTarget == "")
        {
            presumedTarget = null;
        }

        var pressure = IsPressureOn(state, pokemon, move, presumedTarget);
        mon = _resolver.Get(state, pokemon);
        if (!string.IsNullOrEmpty(overriddenMove))
        {
            mon.Moved(move, use: false, reveal: reveal, failed: failed);
            if (!mon.Moves.TryGetValue(ShowdownId.ToMoveId(overriddenMove), out var overridden))
            {
                throw new BattleTrackingException($"Unknown calling move {overriddenMove}");
            }

            overridden.Use(pressure, overridden: true);
        }
        else if (!failed && MOVES_CALLING_OTHERS.Contains(move))
        {
            // Pressure is only counted once the called move is known
            mon.Moved(move, use, reveal, failed: failed);
        }
        else
        {
            mon.Moved(move, use, reveal, pressure, failed);
        }
    }

    private bool IsPressureOn(TrackedBattleState state, string pokemon, string move, string target)
    {
        if (!_dex.TryGetMove(ShowdownId.ToMoveId(move), out var moveData))
        {
            return false;
        }

        TrackedPokemon targetPokemon;
        if (moveData.Target != "all" && target != null)
        {
            targetPokemon = _resolver.Get(state, target);
        }
        else
        {
            targetPokemon = state.PlayerRole == pokemon[..2] ? state.OpponentActivePokemon : state.ActivePokemon;
        }

        return targetPokemon != null
               && targetPokemon.Ability == "pressure"
               && !targetPokemon.Fainted
               && (PRESSURE_TARGETS.Contains(moveData.Target) || moveData.Flags.ContainsKey("mustpressure"));
    }

    private void CheckDamageMessageForItem(TrackedBattleState state, List<string> message)
    {
        if (message.Count == 6 && message[4].StartsWith(FROM_ITEM, StringComparison.Ordinal) &&
            message[5].StartsWith("[of]", StringComparison.Ordinal))
        {
            // Damage from the other side's item (Rocky Helmet): the item belongs to the [of] Pokémon
            var item = message[4].Split("item:")[^1];
            var owner = message[5].Split("[of]")[^1].Trim();
            _resolver.Get(state, owner).SetItem(item);
        }
        else if (message.Count == 5 && message[4].StartsWith(FROM_ITEM, StringComparison.Ordinal))
        {
            _resolver.Get(state, message[2]).SetItem(message[4].Split("item:")[^1]);
        }
    }

    private void CheckDamageMessageForAbility(TrackedBattleState state, List<string> message)
    {
        if (message.Count == 6 && message[4].StartsWith(FROM_ABILITY, StringComparison.Ordinal) &&
            message[5].StartsWith("[of]", StringComparison.Ordinal))
        {
            var ability = message[4].Split("ability:")[^1];
            var owner = message[5].Split("[of]")[^1].Trim();
            _resolver.Get(state, owner).SetAbility(ability);
        }
    }

    private void CheckHealMessageForAbility(TrackedBattleState state, List<string> message)
    {
        if (message.Count == 6 && message[4].StartsWith(FROM_ABILITY, StringComparison.Ordinal))
        {
            var ability = ShowdownId.ToId(message[4].Split("ability:")[^1]);
            var owner = ability == "hospitality" ? message[5].Replace(OF_PREFIX, "").Trim() : message[2];
            _resolver.Get(state, owner).SetAbility(ability);
        }
    }

    private void CheckHealMessageForItem(TrackedBattleState state, List<string> message)
    {
        if (message.Count == 5 && message[4].StartsWith(FROM_ITEM, StringComparison.Ordinal))
        {
            var item = ShowdownId.ToId(message[4].Split("item:")[^1]);
            var pokemon = _resolver.Get(state, message[2]);
            // Berries and herbs are consumed by the time the heal shows up
            if (pokemon.Item != null && !item.Contains("berry") && !item.Contains("herb"))
            {
                pokemon.SetItem(item);
            }
        }
    }

    private void ApplyAbility(TrackedBattleState state, List<string> message)
    {
        RequireLength(message, 4);
        var cause = message[3];
        var mon = _resolver.Get(state, message[2]);
        if (mon.BaseSpecies.Contains("calyrex") && CALYREX_ABILITIES.Contains(cause))
        {
            return;
        }

        var fromTrace = (message.Count > 4 && message[4].StartsWith("[from] ability: Trace", StringComparison.Ordinal))
                        || (message.Count > 5 &&
                            message[5].StartsWith("[from] ability: Trace", StringComparison.Ordinal));
        if (fromTrace)
        {
            if (mon.Ability != "trace")
            {
                if (mon.TemporaryAbility != null)
                {
                    mon.TemporaryAbility = null;
                }
                else if (mon.Ability != null)
                {
                    mon.InnateAbility = null;
                }

                mon.SetAbility("trace");
            }

            mon.SetAbility(cause);
        }
        else if (cause == NEUTRALIZING_GAS)
        {
            FieldStart(state, cause);
        }
        else
        {
            mon.SetAbility(cause);
        }
    }

    private void ApplyStart(TrackedBattleState state, List<string> message)
    {
        RequireLength(message, 4);
        var effect = message[3];
        var mon = _resolver.Get(state, message[2]);
        if (effect == "typechange")
        {
            string types;
            if (message.Count > 5 && message[5].StartsWith(OF_PREFIX, StringComparison.Ordinal))
            {
                // Reflect Type: copies the types of the [of] Pokémon
                types = string.Join('/', _resolver.Get(state, message[5][5..]).Types);
            }
            else
            {
                RequireLength(message, 5);
                types = message[4];
            }

            mon.StartEffect(effect, typeChangeDetails: types);
            return;
        }

        if (effect == "Mimic")
        {
            RequireLength(message, 5);
            mon.MoveSet.MimicMove = _dex.CreateMove(ShowdownId.ToMoveId(message[4]), fromMimic: true);
        }

        mon.StartEffect(effect);
    }

    private void ApplyActivate(TrackedBattleState state, List<string> message)
    {
        RequireLength(message, 4);
        var target = message[2];
        var effect = message[3];
        if (target != "" && effect.Replace(MOVE_PREFIX, "") == "Skill Swap")
        {
            ApplySkillSwap(state, message, target, effect);
        }
        else if (effect == "ability: Dancer")
        {
            _resolver.Get(state, target).Dancing = true;
        }
        else if (effect == "ability: Mummy")
        {
            RequireLength(message, 6);
            var mummified = message[5].Contains(OF_PREFIX) ? message[5].Replace(OF_PREFIX, "") : message[4];
            _resolver.Get(state, mummified).TemporaryAbility = "mummy";
        }
        else if (effect == "ability: Wandering Spirit")
        {
            RequireLength(message, 7);
            _resolver.Get(state, message[2]).TemporaryAbility = message[4];
            _resolver.Get(state, message[6].Replace(OF_PREFIX, "")).TemporaryAbility = "wanderingspirit";
        }
        else if (effect == "ability: Symbiosis")
        {
            RequireLength(message, 6);
            _resolver.Get(state, message[5].Replace(OF_PREFIX, "")).SetItem(message[4].Replace("[item] ", ""));
            _resolver.Get(state, target).SetItem(null);
        }
        else if (effect == "item: Leppa Berry")
        {
            RequireLength(message, 5);
            var mon = _resolver.Get(state, target);
            if (!mon.Moves.TryGetValue(ShowdownId.ToId(message[4]), out var move))
            {
                throw new BattleTrackingException($"Unknown Leppa Berry move {message[4]}");
            }

            move.CurrentPp = Math.Min(move.CurrentPp + 10, move.MaxPp);
        }
        else if (effect == "move: Mimic")
        {
            RequireLength(message, 5);
            _resolver.Get(state, target).MoveSet.MimicMove =
                _dex.CreateMove(ShowdownId.ToMoveId(message[4]), fromMimic: true);
        }
        else if (effect == "move: Trick")
        {
            RequireLength(message, 5);
            var mon = _resolver.Get(state, target);
            var other = _resolver.Get(state, message[4].Replace(OF_PREFIX, ""));
            (mon.Item, other.Item) = (other.Item, mon.Item);
        }
        else if (target != "")
        {
            _resolver.Get(state, target).StartEffect(effect);
        }
    }

    private void ApplySkillSwap(TrackedBattleState state, List<string> message, string target, string effect)
    {
        if (message.Count <= 4)
        {
            return;
        }

        if (message[^1].StartsWith(OF_PREFIX, StringComparison.Ordinal))
        {
            var targetMon = _resolver.Get(state, target);
            var actorMon = _resolver.Get(state, message[^1].Replace(OF_PREFIX, ""));
            var abilities = ParseSkillSwapAbilities(message.Skip(4).Take(message.Count - 5));
            if (abilities.Count >= 2)
            {
                // Opponent swap, gen 5+: both abilities are revealed
                targetMon.StartEffect(effect, skillSwapAbilities: abilities.Take(2).ToList());
                actorMon.TemporaryAbility = abilities[1];
            }
            else if (targetMon.Ability != null && actorMon.Ability != null)
            {
                var targetAbility = targetMon.Ability;
                targetMon.TemporaryAbility = actorMon.Ability;
                actorMon.TemporaryAbility = targetAbility;
            }
        }
        else
        {
            var actorMon = _resolver.Get(state, message[4]);
            var abilities = ParseSkillSwapAbilities(message.Skip(5));
            if (abilities.Count >= 2)
            {
                _resolver.Get(state, target).StartEffect(effect, skillSwapAbilities: abilities.Take(2).ToList());
                actorMon.TemporaryAbility = abilities[1];
            }
        }
    }

    private static List<string> ParseSkillSwapAbilities(IEnumerable<string> parts)
    {
        return parts
            .Select(part => part.Replace("[ability] ", "").Replace("[ability2] ", ""))
            .Where(ability => ability != "")
            .ToList();
    }

    private void ApplyItem(TrackedBattleState state, List<string> message)
    {
        if (message.Count == 6)
        {
            ApplyItemWithSource(state, message);
            return;
        }

        RequireLength(message, 4);
        var item = message[3];
        if (message.Count > 4 && message[4] is "[from] ability: Magician" or "[from] move: Switcheroo" or "[from] move: Trick")
        {
            // The swap was applied by the preceding -activate: only fill in an item we had never seen
            var mon = _resolver.Get(state, message[2]);
            if (mon.Item == TrackedPokemon.UNKNOWN_ITEM)
            {
                mon.SetItem(item);
            }

            return;
        }

        _resolver.Get(state, message[2]).SetItem(item);
    }

    private void ApplyItemWithSource(TrackedBattleState state, List<string> message)
    {
        var item = message[3];
        var cause = message[4];
        switch (cause)
        {
            case "[from] ability: Frisk":
            {
                // Frisk reveals the other active Pokémon's item
                var frisker = _resolver.Get(state, message[5].Split(OF_PREFIX)[^1]);
                if (frisker == state.ActivePokemon)
                {
                    RequirePokemon(state.OpponentActivePokemon).SetItem(item);
                }
                else if (frisker == state.OpponentActivePokemon)
                {
                    RequirePokemon(state.ActivePokemon).SetItem(item);
                }

                frisker.SetAbility("frisk");
                break;
            }
            case "[from] ability: Pickpocket" or "[from] ability: Magician":
            {
                var taker = _resolver.Get(state, message[2]);
                taker.SetItem(item);
                taker.SetAbility(cause == "[from] ability: Pickpocket" ? "pickpocket" : "magician");
                _resolver.Get(state, message[5].Replace(OF_PREFIX, "")).SetItem(null);
                break;
            }
            case "[from] move: Thief" or "[from] move: Covet":
                _resolver.Get(state, message[2]).SetItem(item);
                _resolver.Get(state, message[5].Replace(OF_PREFIX, "")).SetItem(null);
                break;
            default:
                throw new BattleTrackingException($"Unhandled item message: {string.Join('|', message)}");
        }
    }

    private void ApplyPrepare(TrackedBattleState state, List<string> message)
    {
        RequireLength(message, 4);
        TrackedPokemon defender = null;
        if (message.Count >= 5 && message[4] != "[premajor]")
        {
            defender = _resolver.Get(state, message[4]);
            if (ShowdownId.ToId(message[3]) == "skydrop")
            {
                defender.StartEffect("Sky Drop");
            }
        }

        _resolver.Get(state, message[2]).Prepare(message[3], defender != null);
    }

    private void ApplySwapBoost(TrackedBattleState state, List<string> message)
    {
        RequireLength(message, 5);
        var source = _resolver.Get(state, message[2]);
        var target = _resolver.Get(state, message[3]);
        var stats = message[4].Contains("[from]") ? PokeEnvNames.Boosts.ToList() : message[4].Split(", ").ToList();
        foreach (var stat in stats)
        {
            if (!source.Boosts.TryGetValue(stat, out var sourceBoost) ||
                !target.Boosts.TryGetValue(stat, out var targetBoost))
            {
                throw new BattleTrackingException($"Unknown boost {stat}");
            }

            source.Boosts[stat] = targetBoost;
            target.Boosts[stat] = sourceBoost;
        }
    }

    private void ApplyTransform(TrackedBattleState state, List<string> message)
    {
        RequireLength(message, 4);
        var mon = _resolver.Get(state, message[2]);
        if (message.Count > 4 && message[4] == "[from] ability: Imposter")
        {
            mon.AddMove("transform");
            mon.SetAbility("imposter");
        }

        mon.Transform(_resolver.Get(state, message[3]));
    }

    private void ApplyTerastallize(TrackedBattleState state, List<string> message)
    {
        if (message.Count != 4)
        {
            throw new BattleTrackingException("Unexpected -terastallize message");
        }

        var mon = _resolver.Get(state, message[2]);
        mon.Terastallize(message[3]);
        if (state.Team.Contains(mon))
        {
            state.UsedTera = true;
        }
        else if (state.OpponentTeam.Contains(mon))
        {
            state.OpponentUsedTera = true;
        }
    }

    /// <summary>
    /// Battle.end_illusion: a Zoroark's disguise broke. The disguise target goes back to its own state.
    /// </summary>
    private void EndIllusion(TrackedBattleState state, string illusionistIdent, string details)
    {
        var illusioned = illusionistIdent[..2] == state.PlayerRole
            ? state.ActivePokemon
            : state.OpponentActivePokemon;
        if (illusioned == null)
        {
            throw new BattleTrackingException("Cannot end illusion without an active pokemon");
        }

        var illusionist = _resolver.Get(state, illusionistIdent, details: details);
        if (illusionist == illusioned)
        {
            return;
        }

        illusionist.SwitchIn(details);
        illusionist.Status = illusioned.Status;
        // poke-env sets the HP without a status right after, which clears the status again
        illusionist.SetHpStatus($"{illusioned.CurrentHp}/{illusioned.MaxHp}");
        illusioned.WasIllusioned(state.IsNeutralizingGasActive);
    }

    private static void FieldStart(TrackedBattleState state, string message)
    {
        var field = PokeEnvNames.FieldFromMessage(message);
        if (PokeEnvNames.IsTerrain(field))
        {
            foreach (var terrain in state.Fields.Keys.Where(PokeEnvNames.IsTerrain).ToList())
            {
                state.Fields.Remove(terrain);
            }
        }

        state.Fields[field] = state.Turn;
    }

    private static void FieldEnd(TrackedBattleState state, string message)
    {
        var field = PokeEnvNames.FieldFromMessage(message);
        if (field == PokeEnvNames.UNKNOWN)
        {
            return;
        }

        if (!state.Fields.Remove(field) && field != PokeEnvNames.NEUTRALIZING_GAS)
        {
            throw new BattleTrackingException($"Field {field} ended but was not started");
        }
    }

    private static void SideStart(TrackedBattleState state, string side, string message)
    {
        var conditions = side[..2] == state.PlayerRole ? state.SideConditions : state.OpponentSideConditions;
        var condition = PokeEnvNames.SideConditionFromMessage(message);
        if (PokeEnvNames.StackableSideConditions.ContainsKey(condition))
        {
            conditions[condition] = conditions.GetValueOrDefault(condition) + 1;
        }
        else
        {
            conditions.TryAdd(condition, state.Turn);
        }
    }

    private static void SideEnd(TrackedBattleState state, string side, string message)
    {
        var conditions = side[..2] == state.PlayerRole ? state.SideConditions : state.OpponentSideConditions;
        var condition = PokeEnvNames.SideConditionFromMessage(message);
        if (condition != PokeEnvNames.UNKNOWN && !conditions.Remove(condition))
        {
            throw new BattleTrackingException($"Side condition {condition} ended but was not started");
        }
    }

    private static TrackedPokemon RequirePokemon(TrackedPokemon pokemon)
    {
        return pokemon ?? throw new BattleTrackingException("No active Pokémon");
    }

    private static void RequireLength(List<string> message, int length)
    {
        if (message.Count < length)
        {
            throw new BattleTrackingException($"Too few parts in '{string.Join('|', message)}'");
        }
    }

    private static void RemoveLast(List<string> message) => message.RemoveAt(message.Count - 1);

    private static string PopLast(List<string> message)
    {
        var last = message[^1];
        RemoveLast(message);
        return last;
    }
}
