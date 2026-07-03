namespace ElsaMina.Battles.Strategies.Simulation;

public static class StateEvaluator
{
    // Extra value of having a pokemon alive at all, on top of its remaining HP.
    // Evaluating the whole team's HP (not just the active pokemon's) is what makes
    // switching a real trade-off: a switch no longer looks like free healing.
    private const double ALIVE_WEIGHT = 0.3;

    public static double Evaluate(SimulationModel model, SimulationState state)
    {
        var ourScore = 0.0;
        foreach (var hpRatio in state.MemberHpRatios)
        {
            ourScore += hpRatio + (hpRatio > 0 ? ALIVE_WEIGHT : 0.0);
        }

        var opponentScore = state.OpponentHpRatio + (state.OpponentHpRatio > 0 ? ALIVE_WEIGHT : 0.0)
                            + model.OpponentBenchAliveCount * (1.0 + ALIVE_WEIGHT);

        return ourScore - opponentScore;
    }
}
