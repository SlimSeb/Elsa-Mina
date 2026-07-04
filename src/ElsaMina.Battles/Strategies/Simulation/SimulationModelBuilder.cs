using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Logging;
using Lusamine.DamageCalc;
using Lusamine.DamageCalc.Data;

namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// Builds a SimulationModel from the tracked battle state: all damage calc invocations
/// (our moves against the opponent, the opponent's predicted moves against every one of
/// our team members) happen here, once per decision.
/// </summary>
public static class SimulationModelBuilder
{
    public static SimulationModel TryBuild(BattleContext context,
        OpponentPrediction prediction, bool forcedSwitch)
    {
        prediction ??= OpponentPrediction.Empty;
        var opponent = context.ActiveOpponent;
        if (opponent == null ||
            !CalcPokemonFactory.TryBuildOpponentPokemon(opponent, out var opponentPokemon, prediction.Spread))
        {
            return null;
        }

        var opponentMaxHp = opponentPokemon.MaxHP(false);
        if (opponentMaxHp <= 0)
        {
            return null;
        }

        var activeSlot = context.ActiveSlots.Count > 0 ? context.ActiveSlots[0] : null;

        var members = new List<SimulationTeamMember>();
        var memberPokemons = new List<Pokemon>();
        var activeMemberIndex = -1;
        Pokemon terastallizedActivePokemon = null;

        for (var slotIndex = 0; slotIndex < context.SidePokemon.Count; slotIndex++)
        {
            var pokemonState = context.SidePokemon[slotIndex];
            if (pokemonState.IsFainted || pokemonState.CurrentHp <= 0 || pokemonState.MaxHp <= 0 ||
                !CalcPokemonFactory.TryBuildOurPokemon(pokemonState, out var memberPokemon))
            {
                continue;
            }

            var isActive = pokemonState.IsActive && !forcedSwitch;
            Pokemon teraPokemon = null;
            if (isActive && activeSlot != null && !string.IsNullOrEmpty(activeSlot.CanTerastallize) &&
                CalcPokemonFactory.TryBuildOurPokemon(pokemonState, out var teraVariant))
            {
                teraVariant.TeraType = activeSlot.CanTerastallize;
                teraPokemon = teraVariant;
            }

            var moves = isActive && activeSlot != null
                ? BuildActiveMoves(activeSlot, memberPokemon, teraPokemon, opponentPokemon, opponentMaxHp)
                : BuildBenchMoves(pokemonState.Moves, memberPokemon, opponentPokemon, opponentMaxHp);

            if (isActive)
            {
                activeMemberIndex = members.Count;
                terastallizedActivePokemon = teraPokemon;
            }

            members.Add(new SimulationTeamMember
            {
                TeamSlot = slotIndex + 1,
                Species = CalcPokemonFactory.ExtractSpeciesFromDetails(pokemonState.Details),
                InitialHpRatio = (double)pokemonState.CurrentHp / pokemonState.MaxHp,
                Speed = ComputeOurSpeed(pokemonState),
                SwitchInChipRatio = ComputeSwitchInChip(memberPokemon.Types,
                    context.OwnSideStealthRock, context.OwnSideSpikesLayers),
                Moves = moves
            });
            memberPokemons.Add(memberPokemon);
        }

        if (members.Count == 0)
        {
            return null;
        }

        var opponentMoves = BuildOpponentMoves(prediction.Moves, opponentPokemon, members,
            memberPokemons, terastallizedActivePokemon, activeMemberIndex);

        return new SimulationModel
        {
            Members = members,
            ActiveMemberIndex = activeMemberIndex,
            CanTerastallize = terastallizedActivePokemon != null,
            ActiveIsTrapped = activeSlot?.Trapped ?? false,
            OpponentMoves = opponentMoves,
            OpponentSpeed = ComputeOpponentSpeed(opponent, opponentPokemon),
            OpponentHpRatio = opponent.HpPercent / 100.0,
            OpponentBenchAliveCount = context.OpponentPokemon.Count(pokemon => !pokemon.IsFainted && !pokemon.IsActive),
            OpponentIsPassive = ComputeOpponentIsPassive(opponentMoves, activeMemberIndex),
            InitialOpponentField = new OpponentFieldConditions
            {
                StealthRock = context.OpponentSideStealthRock,
                SpikesLayers = context.OpponentSideSpikesLayers,
                ToxicSpikes = context.OpponentSideToxicSpikes,
                StickyWeb = context.OpponentSideStickyWeb,
                Taunted = context.OpponentActiveTaunted
            }
        };
    }

    private static List<SimulationMove> BuildActiveMoves(BattleActiveSlot slot, Pokemon attacker,
        Pokemon teraAttacker, Pokemon defender, int defenderMaxHp)
    {
        var moves = new List<SimulationMove>();
        for (var index = 0; index < slot.Moves.Count; index++)
        {
            var moveState = slot.Moves[index];
            var isUsable = moveState.Name == "Recharge" || moveState.MaxPp == 0 ||
                           (!moveState.IsDisabled && moveState.Pp > 0);
            if (!isUsable)
            {
                continue;
            }

            var move = BuildSimulationMove(moveState.Name, index + 1, attacker, teraAttacker,
                defender, defenderMaxHp);
            if (move != null)
            {
                moves.Add(move);
            }
        }

        return moves;
    }

    private static List<SimulationMove> BuildBenchMoves(List<string> moveNames, Pokemon attacker,
        Pokemon defender, int defenderMaxHp)
    {
        var moves = new List<SimulationMove>();
        foreach (var moveName in moveNames)
        {
            var move = BuildSimulationMove(moveName, 0, attacker, null, defender, defenderMaxHp);
            if (move != null)
            {
                moves.Add(move);
            }
        }

        return moves;
    }

    private static SimulationMove BuildSimulationMove(string moveName, int requestMoveIndex,
        Pokemon attacker, Pokemon teraAttacker, Pokemon defender, int defenderMaxHp)
    {
        try
        {
            var calcMove = new Move(CalcPokemonFactory.Generation, moveName);
            var isStatus = calcMove.Category == MoveCategories.Status;

            return new SimulationMove
            {
                Name = calcMove.Name,
                RequestMoveIndex = requestMoveIndex,
                Priority = calcMove.Priority,
                IsStatus = isStatus,
                StatusEffect = isStatus ? DetermineStatusEffect(calcMove.Name) : StatusMoveEffect.None,
                DamageRatio = isStatus ? 0.0 : ComputeExpectedDamageRatio(attacker, defender, calcMove, defenderMaxHp),
                TeraDamageRatio = isStatus || teraAttacker == null
                    ? null
                    : ComputeExpectedDamageRatio(teraAttacker, defender, calcMove, defenderMaxHp)
            };
        }
        catch (Exception exception)
        {
            Log.Debug("Could not build simulation move {Move}: {Message}", moveName, exception.Message);
            return null;
        }
    }

    private static List<OpponentSimulationMove> BuildOpponentMoves(
        IReadOnlyList<PredictedMove> predictedMoves, Pokemon opponentPokemon,
        List<SimulationTeamMember> members, List<Pokemon> memberPokemons,
        Pokemon terastallizedActivePokemon, int activeMemberIndex)
    {
        var opponentMoves = new List<OpponentSimulationMove>();
        if (predictedMoves == null)
        {
            return opponentMoves;
        }

        foreach (var predictedMove in predictedMoves)
        {
            try
            {
                var calcMove = new Move(CalcPokemonFactory.Generation, predictedMove.Name);
                if (calcMove.Category == MoveCategories.Status)
                {
                    continue;
                }

                var damageToMembers = new double[members.Count];
                for (var memberIndex = 0; memberIndex < members.Count; memberIndex++)
                {
                    var memberMaxHp = memberPokemons[memberIndex].MaxHP(false);
                    damageToMembers[memberIndex] = ComputeExpectedDamageRatio(opponentPokemon,
                        memberPokemons[memberIndex], calcMove, memberMaxHp);
                }

                var damageToTeraActive = activeMemberIndex >= 0
                    ? damageToMembers[activeMemberIndex]
                    : 0.0;
                if (terastallizedActivePokemon != null)
                {
                    damageToTeraActive = ComputeExpectedDamageRatio(opponentPokemon,
                        terastallizedActivePokemon, calcMove, terastallizedActivePokemon.MaxHP(false));
                }

                opponentMoves.Add(new OpponentSimulationMove
                {
                    Name = calcMove.Name,
                    Priority = calcMove.Priority,
                    Probability = predictedMove.Probability,
                    DamageToMembers = damageToMembers,
                    DamageToTerastallizedActive = damageToTeraActive
                });
            }
            catch (Exception exception)
            {
                Log.Debug("Could not build opponent simulation move {Move}: {Message}",
                    predictedMove.Name, exception.Message);
            }
        }

        return opponentMoves;
    }

    private static double ComputeExpectedDamageRatio(Pokemon attacker, Pokemon defender,
        Move move, int defenderMaxHp)
    {
        if (defenderMaxHp <= 0)
        {
            return 0.0;
        }

        try
        {
            var result = Calc.Calculate(CalcPokemonFactory.Generation, attacker, defender, move, null);
            var (minDamage, maxDamage) = result.Range();
            return (minDamage + maxDamage) / 2.0 / defenderMaxHp;
        }
        catch
        {
            // Move not in the dex or not a damaging move
            return 0.0;
        }
    }

    // The opponent's active pokemon counts as passive when its best expected hit on us stays below this
    private const double PASSIVE_OPPONENT_DAMAGE_THRESHOLD = 0.25;

    private static StatusMoveEffect DetermineStatusEffect(string moveName)
    {
        var id = new string(moveName.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return id switch
        {
            "stealthrock" => StatusMoveEffect.StealthRock,
            "spikes" => StatusMoveEffect.Spikes,
            "toxicspikes" => StatusMoveEffect.ToxicSpikes,
            "stickyweb" => StatusMoveEffect.StickyWeb,
            "taunt" => StatusMoveEffect.Taunt,
            _ => StatusMoveEffect.OtherStatus
        };
    }

    private static bool ComputeOpponentIsPassive(List<OpponentSimulationMove> opponentMoves, int activeMemberIndex)
    {
        // All-status opponents build no damaging moves at all, so they are trivially passive
        if (opponentMoves.Count == 0 || activeMemberIndex < 0)
        {
            return opponentMoves.Count == 0;
        }

        var bestDamageToActive = opponentMoves.Max(move => move.DamageToMembers[activeMemberIndex]);
        return bestDamageToActive < PASSIVE_OPPONENT_DAMAGE_THRESHOLD;
    }

    private static readonly double[] SPIKES_CHIP_BY_LAYERS = [0.0, 1.0 / 8.0, 1.0 / 6.0, 1.0 / 4.0];

    private static double ComputeSwitchInChip(IReadOnlyList<string> types, bool stealthRock, int spikesLayers)
    {
        var chip = 0.0;

        if (stealthRock)
        {
            // Stealth Rock ignores grounding and scales with the Rock type's effectiveness
            chip += 0.125 * Data.TypeMatchupTable.GetMultiplier("Rock", types);
        }

        if (spikesLayers > 0 && IsGrounded(types))
        {
            chip += SPIKES_CHIP_BY_LAYERS[Math.Clamp(spikesLayers, 0, 3)];
        }

        return chip;
    }

    private static bool IsGrounded(IReadOnlyList<string> types)
    {
        // Approximation: only Flying types are treated as airborne.
        // Levitate and Air Balloon are not tracked, so they are not accounted for here.
        return types == null || !types.Any(type => type.Equals("Flying", StringComparison.OrdinalIgnoreCase));
    }

    private static int ComputeOurSpeed(BattlePokemonState state)
    {
        var speed = state.Stats.Spe;
        if (CalcPokemonFactory.ExtractStatus(state.Condition) == "par")
        {
            speed /= 2;
        }

        return speed;
    }

    private static int ComputeOpponentSpeed(OpponentPokemonState state, Pokemon opponentPokemon)
    {
        // The opponent's exact spread is unknown: use the calc's default spread as an estimate
        var speed = (double)opponentPokemon.RawStats.Spe;

        if (state.Boosts.TryGetValue("spe", out var boostStage) && boostStage != 0)
        {
            speed *= boostStage > 0 ? (2.0 + boostStage) / 2.0 : 2.0 / (2.0 - boostStage);
        }

        if (state.Status == "par")
        {
            speed /= 2;
        }

        return (int)speed;
    }
}
