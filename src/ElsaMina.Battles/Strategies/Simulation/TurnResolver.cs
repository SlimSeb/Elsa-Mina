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
            // The switch resolves first, then the opponent's move hits the incoming pokemon
            var incomingIndex = ourAction.MemberIndex;
            if (opponentMove != null && opponentHpRatio > 0)
            {
                memberHpRatios[incomingIndex] = Math.Max(0.0,
                    memberHpRatios[incomingIndex] - GetOpponentDamage(model, state, opponentMove, incomingIndex));
            }

            return state with { ActiveMemberIndex = incomingIndex, MemberHpRatios = memberHpRatios };
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

        if (WeActFirst(actingMember, move, model, opponentMove))
        {
            opponentHpRatio = Math.Max(0.0, opponentHpRatio - ourDamage);
            if (opponentHpRatio > 0)
            {
                memberHpRatios[actingIndex] = Math.Max(0.0, memberHpRatios[actingIndex] - incomingDamage);
            }
        }
        else
        {
            memberHpRatios[actingIndex] = Math.Max(0.0, memberHpRatios[actingIndex] - incomingDamage);
            if (memberHpRatios[actingIndex] > 0)
            {
                opponentHpRatio = Math.Max(0.0, opponentHpRatio - ourDamage);
            }
        }

        return state with
        {
            MemberHpRatios = memberHpRatios,
            OpponentHpRatio = opponentHpRatio,
            HasTerastallized = state.HasTerastallized || ourAction.UseTerastallize,
            RootActiveIsTerastallized = state.RootActiveIsTerastallized || ourAction.UseTerastallize
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
