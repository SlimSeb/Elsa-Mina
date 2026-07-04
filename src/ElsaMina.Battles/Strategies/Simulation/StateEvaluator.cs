namespace ElsaMina.Battles.Strategies.Simulation;

public static class StateEvaluator
{
    // Extra value of having a pokemon alive at all, on top of its remaining HP.
    // Evaluating the whole team's HP (not just the active pokemon's) is what makes
    // switching a real trade-off: a switch no longer looks like free healing.
    private const double ALIVE_WEIGHT = 0.3;

    // Per-turn pressure exerted by hazards we have set on the opponent's side, expressed per opponent
    // pokemon still on the bench (each will eventually switch in and take the chip / speed drop). These
    // accrue every turn the hazard stays up, so setting hazards early beats setting them late, and they
    // are near-worthless once the opponent is down to its last pokemon.
    private const double STEALTH_ROCK_PRESSURE_PER_MON = 0.037;
    private const double SPIKES_PRESSURE_PER_LAYER_PER_MON = 0.020;
    private const double TOXIC_SPIKES_PRESSURE_PER_MON = 0.023;
    private const double STICKY_WEB_PRESSURE_PER_MON = 0.017;

    // Per-turn value of keeping a passive opponent taunted, denying it its status game plan. Kept small
    // so Taunt does not out-value real attacking options: it accrues each turn the taunt is up (capped by
    // the short search horizon), which makes taunting early strictly better than deferring it, and makes
    // re-taunting an already taunted opponent worthless (every action accrues the same, so attacking wins).
    private const double TAUNT_PRESSURE = 0.08;

    public static double Evaluate(SimulationModel model, SimulationState state)
    {
        var ourScore = 0.0;
        foreach (var hpRatio in state.MemberHpRatios)
        {
            ourScore += hpRatio + (hpRatio > 0 ? ALIVE_WEIGHT : 0.0);
        }

        var opponentScore = state.OpponentHpRatio + (state.OpponentHpRatio > 0 ? ALIVE_WEIGHT : 0.0)
                            + model.OpponentBenchAliveCount * (1.0 + ALIVE_WEIGHT);

        return ourScore - opponentScore + state.AccruedFieldValue;
    }

    /// <summary>
    /// Value the opponent-side board conditions (entry hazards and Taunt) exert during a single turn.
    /// Accumulated into the state each turn by the turn resolver so that longer-standing conditions are
    /// worth more, which makes setting or applying them earlier strictly better than deferring.
    /// </summary>
    public static double PerTurnFieldPressure(SimulationModel model, OpponentFieldConditions field)
    {
        var switchInsLeft = model.OpponentBenchAliveCount;
        var pressure = 0.0;

        if (switchInsLeft > 0)
        {
            if (field.StealthRock)
            {
                pressure += STEALTH_ROCK_PRESSURE_PER_MON * switchInsLeft;
            }

            pressure += SPIKES_PRESSURE_PER_LAYER_PER_MON * field.SpikesLayers * switchInsLeft;

            if (field.ToxicSpikes)
            {
                pressure += TOXIC_SPIKES_PRESSURE_PER_MON * switchInsLeft;
            }

            if (field.StickyWeb)
            {
                pressure += STICKY_WEB_PRESSURE_PER_MON * switchInsLeft;
            }
        }

        // Taunt is only worth spending a turn on when the opponent relies on status moves
        if (field.Taunted && model.OpponentIsPassive)
        {
            pressure += TAUNT_PRESSURE;
        }

        return pressure;
    }
}
