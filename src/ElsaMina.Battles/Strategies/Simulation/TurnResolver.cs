namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// Resolves one simulated turn where both sides act simultaneously:
/// switches happen before moves, then moves execute in priority/speed order,
/// and a fainted pokemon does not get to retaliate.
/// </summary>
public static class TurnResolver
{
    public static bool IsTerminal(SimulationState state)
    {
        return state.OpponentHpRatio <= 0 || state.MemberHpRatios.All(hpRatio => hpRatio <= 0);
    }

    public static List<SimulationAction> EnumerateOurActions(SimulationModel model, SimulationState state)
    {
        var actions = new List<SimulationAction>();
        var activeIndex = state.ActiveMemberIndex;
        var activeIsAlive = activeIndex >= 0 && state.MemberHpRatios[activeIndex] > 0;

        // Moves are enumerated before switches so that on equal search value we attack instead of switching
        if (activeIsAlive)
        {
            var activeMember = model.Members[activeIndex];
            var canTerastallize = model.CanTerastallize && !state.HasTerastallized &&
                                  activeIndex == model.ActiveMemberIndex;

            for (var moveIndex = 0; moveIndex < activeMember.Moves.Count; moveIndex++)
            {
                actions.Add(new SimulationAction(SimulationActionKind.Move, activeIndex, moveIndex));
                if (canTerastallize && activeMember.Moves[moveIndex].TeraDamageRatio.HasValue)
                {
                    actions.Add(new SimulationAction(SimulationActionKind.Move, activeIndex, moveIndex,
                        UseTerastallize: true));
                }
            }
        }

        var isTrappedAtRoot = model.ActiveIsTrapped && activeIndex == model.ActiveMemberIndex;
        if (!activeIsAlive || !isTrappedAtRoot)
        {
            for (var memberIndex = 0; memberIndex < model.Members.Count; memberIndex++)
            {
                if (memberIndex != activeIndex && state.MemberHpRatios[memberIndex] > 0)
                {
                    actions.Add(new SimulationAction(SimulationActionKind.Switch, memberIndex));
                }
            }
        }

        return actions;
    }

    public static SimulationState Resolve(SimulationModel model, SimulationState state,
        SimulationAction ourAction, int opponentMoveIndex)
    {
        var memberHpRatios = (double[])state.MemberHpRatios.Clone();
        var opponentHpRatio = state.OpponentHpRatio;
        var opponentMove = model.OpponentMoves.Count > 0 ? model.OpponentMoves[opponentMoveIndex] : null;

        if (ourAction.Kind == SimulationActionKind.Switch)
        {
            // Entry hazards chip the incoming pokemon as it enters, then the opponent's move hits it
            var incomingIndex = ourAction.MemberIndex;
            memberHpRatios[incomingIndex] = Math.Max(0.0,
                memberHpRatios[incomingIndex] - model.Members[incomingIndex].SwitchInChipRatio);

            if (opponentMove != null && opponentHpRatio > 0 && memberHpRatios[incomingIndex] > 0)
            {
                memberHpRatios[incomingIndex] = Math.Max(0.0,
                    memberHpRatios[incomingIndex] - GetOpponentDamage(model, state, opponentMove, incomingIndex));
            }

            // Hazards already up keep exerting pressure this turn even though we only switched
            return state with
            {
                ActiveMemberIndex = incomingIndex,
                MemberHpRatios = memberHpRatios,
                AccruedFieldValue = state.AccruedFieldValue
                                    + StateEvaluator.PerTurnFieldPressure(model, state.OpponentField)
            };
        }

        var actingIndex = ourAction.MemberIndex;
        var actingMember = model.Members[actingIndex];
        var move = actingMember.Moves[ourAction.MoveListIndex];

        var isTerastallized = ourAction.UseTerastallize ||
                              (state.RootActiveIsTerastallized && actingIndex == model.ActiveMemberIndex);
        var ourDamage = isTerastallized && move.TeraDamageRatio.HasValue
            ? move.TeraDamageRatio.Value
            : move.DamageRatio;

        var incomingDamage = 0.0;
        if (opponentMove != null)
        {
            incomingDamage = isTerastallized && actingIndex == model.ActiveMemberIndex
                ? opponentMove.DamageToTerastallizedActive
                : opponentMove.DamageToMembers[actingIndex];
        }

        bool ourMoveLands;
        if (WeActFirst(actingMember, move, model, opponentMove))
        {
            // We move first, so our move always executes; the opponent only retaliates if it survives
            ourMoveLands = true;
            opponentHpRatio = Math.Max(0.0, opponentHpRatio - ourDamage);
            if (opponentHpRatio > 0)
            {
                memberHpRatios[actingIndex] = Math.Max(0.0, memberHpRatios[actingIndex] - incomingDamage);
            }
        }
        else
        {
            memberHpRatios[actingIndex] = Math.Max(0.0, memberHpRatios[actingIndex] - incomingDamage);
            // Our move only lands if we survive the opponent's hit
            ourMoveLands = memberHpRatios[actingIndex] > 0;
            if (ourMoveLands)
            {
                opponentHpRatio = Math.Max(0.0, opponentHpRatio - ourDamage);
            }
        }

        var opponentField = ourMoveLands && move.StatusEffect != StatusMoveEffect.None
            ? ApplyStatusEffect(state.OpponentField, move.StatusEffect)
            : state.OpponentField;

        return state with
        {
            MemberHpRatios = memberHpRatios,
            OpponentHpRatio = opponentHpRatio,
            HasTerastallized = state.HasTerastallized || ourAction.UseTerastallize,
            RootActiveIsTerastallized = state.RootActiveIsTerastallized || ourAction.UseTerastallize,
            OpponentField = opponentField,
            // Accrue this turn's pressure from the resulting board (hazards / taunt set now count from
            // this turn on, so setting them earlier is strictly better than deferring)
            AccruedFieldValue = state.AccruedFieldValue + StateEvaluator.PerTurnFieldPressure(model, opponentField)
        };
    }

    private static OpponentFieldConditions ApplyStatusEffect(OpponentFieldConditions field,
        StatusMoveEffect effect)
    {
        return effect switch
        {
            StatusMoveEffect.StealthRock => field with { StealthRock = true },
            StatusMoveEffect.Spikes => field with { SpikesLayers = Math.Min(3, field.SpikesLayers + 1) },
            StatusMoveEffect.ToxicSpikes => field with { ToxicSpikes = true },
            StatusMoveEffect.StickyWeb => field with { StickyWeb = true },
            StatusMoveEffect.Taunt => field with { Taunted = true },
            _ => field
        };
    }

    private static double GetOpponentDamage(SimulationModel model, SimulationState state,
        OpponentSimulationMove opponentMove, int targetIndex)
    {
        return state.RootActiveIsTerastallized && targetIndex == model.ActiveMemberIndex
            ? opponentMove.DamageToTerastallizedActive
            : opponentMove.DamageToMembers[targetIndex];
    }

    private static bool WeActFirst(SimulationTeamMember actingMember, SimulationMove move,
        SimulationModel model, OpponentSimulationMove opponentMove)
    {
        if (opponentMove == null)
        {
            return true;
        }

        if (move.Priority != opponentMove.Priority)
        {
            return move.Priority > opponentMove.Priority;
        }

        // Speed tie is resolved in the opponent's favor to stay pessimistic
        return actingMember.Speed > model.OpponentSpeed;
    }
}
