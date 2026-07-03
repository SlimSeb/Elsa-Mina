using ElsaMina.Battles.Strategies.Simulation;

namespace ElsaMina.UnitTests.Battles.Strategies.Simulation;

public static class SimulationModelTestFactory
{
    public static SimulationMove CreateMove(string name, double damageRatio, int priority = 0,
        int requestMoveIndex = 1, double? teraDamageRatio = null)
    {
        return new SimulationMove
        {
            Name = name,
            RequestMoveIndex = requestMoveIndex,
            Priority = priority,
            DamageRatio = damageRatio,
            TeraDamageRatio = teraDamageRatio
        };
    }

    public static SimulationTeamMember CreateMember(int teamSlot, double hpRatio, int speed,
        params SimulationMove[] moves)
    {
        return new SimulationTeamMember
        {
            TeamSlot = teamSlot,
            Species = $"Member{teamSlot}",
            InitialHpRatio = hpRatio,
            Speed = speed,
            Moves = moves.ToList()
        };
    }

    public static OpponentSimulationMove CreateOpponentMove(string name, double[] damageToMembers,
        int priority = 0, double damageToTerastallizedActive = 0.0)
    {
        return new OpponentSimulationMove
        {
            Name = name,
            Priority = priority,
            Probability = 1.0,
            DamageToMembers = damageToMembers,
            DamageToTerastallizedActive = damageToTerastallizedActive
        };
    }

    public static SimulationModel CreateModel(int activeMemberIndex,
        List<SimulationTeamMember> members,
        List<OpponentSimulationMove> opponentMoves,
        int opponentSpeed,
        double opponentHpRatio = 1.0,
        bool canTerastallize = false,
        bool activeIsTrapped = false)
    {
        return new SimulationModel
        {
            Members = members,
            ActiveMemberIndex = activeMemberIndex,
            CanTerastallize = canTerastallize,
            ActiveIsTrapped = activeIsTrapped,
            OpponentMoves = opponentMoves,
            OpponentSpeed = opponentSpeed,
            OpponentHpRatio = opponentHpRatio,
            OpponentBenchAliveCount = 0
        };
    }
}
