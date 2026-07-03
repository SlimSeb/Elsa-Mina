using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Battles.Strategies.Simulation;
using ElsaMina.UnitTests.Battles.Strategies.Simulation;

namespace ElsaMina.UnitTests.Battles.Strategies.Search;

public class MinimaxSearchTest
{
    private MinimaxSearch _minimaxSearch;

    [SetUp]
    public void SetUp()
    {
        _minimaxSearch = new MinimaxSearch();
    }

    [Test]
    public void Test_FindBestAction_ShouldPickTheKnockOutMove_WhenAvailable()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 200,
                SimulationModelTestFactory.CreateMove("Weak Move", 0.3, requestMoveIndex: 1),
                SimulationModelTestFactory.CreateMove("Knock Out Move", 1.0, requestMoveIndex: 2))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.4])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(action.Kind, Is.EqualTo(SimulationActionKind.Move));
            Assert.That(members[0].Moves[action.MoveListIndex].Name, Is.EqualTo("Knock Out Move"));
        }
    }

    [Test]
    public void Test_FindBestAction_ShouldAttack_WhenTradingIsFavorable()
    {
        // Arrange - the active pokemon wins the 1v1; switching would only sack HP of the bench.
        // The old evaluation looked at the active pokemon's HP only, making the fresh
        // bench pokemon look like free healing and causing constant switching.
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 0.7, speed: 200,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.6)),
            SimulationModelTestFactory.CreateMember(2, 1.0, speed: 80,
                SimulationModelTestFactory.CreateMove("Bench Move", 0.3))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.3, 0.3])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        Assert.That(action.Kind, Is.EqualTo(SimulationActionKind.Move));
    }

    [Test]
    public void Test_FindBestAction_ShouldSwitch_WhenActivePokemonIsDoomed()
    {
        // Arrange - the active pokemon is slower and gets knocked out before moving,
        // while the bench pokemon barely takes damage and wins the matchup
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 0.5, speed: 50,
                SimulationModelTestFactory.CreateMove("Weak Move", 0.15)),
            SimulationModelTestFactory.CreateMember(2, 1.0, speed: 150,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.5))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Threatening Move", [0.6, 0.1])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(action.Kind, Is.EqualTo(SimulationActionKind.Switch));
            Assert.That(action.MemberIndex, Is.EqualTo(1));
        }
    }

    [Test]
    public void Test_FindBestAction_ShouldReturnSwitch_WhenForcedToSwitch()
    {
        // Arrange - no active pokemon: the search must pick the best switch-in
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 100,
                SimulationModelTestFactory.CreateMove("Weak Move", 0.2)),
            SimulationModelTestFactory.CreateMember(2, 1.0, speed: 150,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.6))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Threatening Move", [0.5, 0.1])
        };
        var model = SimulationModelTestFactory.CreateModel(-1, members, opponentMoves, opponentSpeed: 100);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(action.Kind, Is.EqualTo(SimulationActionKind.Switch));
            Assert.That(action.MemberIndex, Is.EqualTo(1));
        }
    }

    [Test]
    public void Test_FindBestAction_ShouldReturnNull_WhenNoActionIsAvailable()
    {
        // Arrange
        var model = SimulationModelTestFactory.CreateModel(-1, [], [], opponentSpeed: 100);

        // Act & Assert
        Assert.That(_minimaxSearch.FindBestAction(model), Is.Null);
    }

    [Test]
    public void Test_FindBestAction_ShouldTerastallize_WhenItSecuresTheKnockOut()
    {
        // Arrange - only the tera-boosted move knocks the opponent out before it can retaliate
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 0.4, speed: 200,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.7, teraDamageRatio: 1.0))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.5],
                damageToTerastallizedActive: 0.5)
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100,
            canTerastallize: true);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        Assert.That(action.UseTerastallize, Is.True);
    }
}
