using ElsaMina.Battles.Strategies.Simulation;

namespace ElsaMina.UnitTests.Battles.Strategies.Simulation;

public class StateEvaluatorTest
{
    [Test]
    public void Test_Evaluate_ShouldReturnSameScore_WhenOnlyActiveIndexDiffers()
    {
        // Arrange - switching alone must not change the evaluation (no free healing)
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 0.4, speed: 100),
            SimulationModelTestFactory.CreateMember(2, 1.0, speed: 80)
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, [], opponentSpeed: 100);
        var stateWithFirstActive = model.CreateInitialState();
        var stateWithSecondActive = stateWithFirstActive with { ActiveMemberIndex = 1 };

        // Act
        var firstScore = StateEvaluator.Evaluate(model, stateWithFirstActive);
        var secondScore = StateEvaluator.Evaluate(model, stateWithSecondActive);

        // Assert
        Assert.That(firstScore, Is.EqualTo(secondScore));
    }

    [Test]
    public void Test_Evaluate_ShouldDecrease_WhenOurTeamLosesHp()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 100)
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, [], opponentSpeed: 100);
        var healthyState = model.CreateInitialState();
        var damagedState = healthyState with { MemberHpRatios = [0.5] };

        // Act & Assert
        Assert.That(StateEvaluator.Evaluate(model, damagedState),
            Is.LessThan(StateEvaluator.Evaluate(model, healthyState)));
    }

    [Test]
    public void Test_Evaluate_ShouldIncrease_WhenOpponentIsKnockedOut()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 100)
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, [], opponentSpeed: 100);
        var initialState = model.CreateInitialState();
        var opponentKnockedOutState = initialState with { OpponentHpRatio = 0.0 };

        // Act & Assert
        Assert.That(StateEvaluator.Evaluate(model, opponentKnockedOutState),
            Is.GreaterThan(StateEvaluator.Evaluate(model, initialState)));
    }
}
