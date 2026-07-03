using ElsaMina.Battles.Strategies.Simulation;

namespace ElsaMina.UnitTests.Battles.Strategies.Simulation;

public class TurnResolverTest
{
    private static readonly SimulationAction FIRST_MOVE_ACTION =
        new(SimulationActionKind.Move, MemberIndex: 0, MoveListIndex: 0);

    [Test]
    public void Test_Resolve_ShouldPreventOpponentRetaliation_WhenFasterMoveKnocksOut()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 200,
                SimulationModelTestFactory.CreateMove("Strong Move", 1.0))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.5])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);

        // Act
        var result = TurnResolver.Resolve(model, model.CreateInitialState(), FIRST_MOVE_ACTION, 0);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.OpponentHpRatio, Is.EqualTo(0.0));
            Assert.That(result.MemberHpRatios[0], Is.EqualTo(1.0));
        }
    }

    [Test]
    public void Test_Resolve_ShouldPreventOurMove_WhenSlowerAndKnockedOutFirst()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 0.4, speed: 50,
                SimulationModelTestFactory.CreateMove("Strong Move", 1.0))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.5])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);

        // Act
        var result = TurnResolver.Resolve(model, model.CreateInitialState(), FIRST_MOVE_ACTION, 0);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.MemberHpRatios[0], Is.EqualTo(0.0));
            Assert.That(result.OpponentHpRatio, Is.EqualTo(1.0));
        }
    }

    [Test]
    public void Test_Resolve_ShouldLetUsActFirst_WhenSlowerButUsingPriorityMove()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 0.4, speed: 50,
                SimulationModelTestFactory.CreateMove("Priority Move", 1.0, priority: 1))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.5])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);

        // Act
        var result = TurnResolver.Resolve(model, model.CreateInitialState(), FIRST_MOVE_ACTION, 0);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.OpponentHpRatio, Is.EqualTo(0.0));
            Assert.That(result.MemberHpRatios[0], Is.EqualTo(0.4));
        }
    }

    [Test]
    public void Test_Resolve_ShouldLetOpponentActFirst_WhenSpeedsAreTied()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 0.4, speed: 100,
                SimulationModelTestFactory.CreateMove("Strong Move", 1.0))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.5])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);

        // Act
        var result = TurnResolver.Resolve(model, model.CreateInitialState(), FIRST_MOVE_ACTION, 0);

        // Assert
        Assert.That(result.MemberHpRatios[0], Is.EqualTo(0.0));
    }

    [Test]
    public void Test_Resolve_ShouldMakeIncomingPokemonTakeTheHit_WhenSwitching()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 100,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.5)),
            SimulationModelTestFactory.CreateMember(2, 1.0, speed: 80,
                SimulationModelTestFactory.CreateMove("Other Move", 0.5))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.5, 0.2])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);
        var switchAction = new SimulationAction(SimulationActionKind.Switch, MemberIndex: 1);

        // Act
        var result = TurnResolver.Resolve(model, model.CreateInitialState(), switchAction, 0);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ActiveMemberIndex, Is.EqualTo(1));
            Assert.That(result.MemberHpRatios[1], Is.EqualTo(0.8).Within(1e-9));
            Assert.That(result.MemberHpRatios[0], Is.EqualTo(1.0));
            Assert.That(result.OpponentHpRatio, Is.EqualTo(1.0));
        }
    }

    [Test]
    public void Test_Resolve_ShouldApplyHazardChip_WhenSwitchingUnderEntryHazards()
    {
        // Arrange - the incoming pokemon loses 25% to Stealth Rock before the opponent's move lands
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 100,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.5)),
            new()
            {
                TeamSlot = 2,
                Species = "Member2",
                InitialHpRatio = 1.0,
                Speed = 80,
                SwitchInChipRatio = 0.25,
                Moves = [SimulationModelTestFactory.CreateMove("Other Move", 0.3)]
            }
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.5, 0.2])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);
        var switchAction = new SimulationAction(SimulationActionKind.Switch, MemberIndex: 1);

        // Act
        var result = TurnResolver.Resolve(model, model.CreateInitialState(), switchAction, 0);

        // Assert - 25% hazard chip then 20% from the opponent's move
        Assert.That(result.MemberHpRatios[1], Is.EqualTo(0.55).Within(1e-9));
    }

    [Test]
    public void Test_EnumerateOurActions_ShouldOnlyReturnSwitches_WhenActivePokemonFainted()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 100,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.5)),
            SimulationModelTestFactory.CreateMember(2, 1.0, speed: 80,
                SimulationModelTestFactory.CreateMove("Other Move", 0.5))
        };
        var model = SimulationModelTestFactory.CreateModel(-1, members, [], opponentSpeed: 100);

        // Act
        var actions = TurnResolver.EnumerateOurActions(model, model.CreateInitialState());

        // Assert
        Assert.That(actions, Has.Count.EqualTo(2));
        Assert.That(actions.All(action => action.Kind == SimulationActionKind.Switch), Is.True);
    }

    [Test]
    public void Test_EnumerateOurActions_ShouldNotReturnSwitches_WhenActivePokemonIsTrapped()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 100,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.5)),
            SimulationModelTestFactory.CreateMember(2, 1.0, speed: 80,
                SimulationModelTestFactory.CreateMove("Other Move", 0.5))
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, [], opponentSpeed: 100,
            activeIsTrapped: true);

        // Act
        var actions = TurnResolver.EnumerateOurActions(model, model.CreateInitialState());

        // Assert
        Assert.That(actions.All(action => action.Kind == SimulationActionKind.Move), Is.True);
    }

    [Test]
    public void Test_EnumerateOurActions_ShouldIncludeTeraVariants_WhenTerastallizationIsAvailable()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 100,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.5, teraDamageRatio: 0.8))
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, [], opponentSpeed: 100,
            canTerastallize: true);

        // Act
        var actions = TurnResolver.EnumerateOurActions(model, model.CreateInitialState());

        // Assert
        Assert.That(actions, Has.Count.EqualTo(2));
        Assert.That(actions.Count(action => action.UseTerastallize), Is.EqualTo(1));
    }
}
