using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Battles.Strategies.Simulation;
using ElsaMina.UnitTests.Battles.Strategies.Simulation;

namespace ElsaMina.UnitTests.Battles.Strategies.Search;

/// <summary>
/// Behavioural tests for the status-move value model: hazards should be set when they are not yet
/// up and the opponent has a team left to switch in, and Taunt should be used against a passive
/// opponent. Both must still lose out to a lethal attacking move.
/// </summary>
public class StatusMoveSearchTest
{
    private MinimaxSearch _minimaxSearch;

    [SetUp]
    public void SetUp()
    {
        _minimaxSearch = new MinimaxSearch();
    }

    [Test]
    public void Test_FindBestAction_ShouldSetStealthRock_WhenNotUpAndOpponentHasATeam()
    {
        // Arrange - our attacks only chip the healthy opponent, but rocks tax its five benched mons
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 120,
                SimulationModelTestFactory.CreateMove("Weak Attack", 0.15, requestMoveIndex: 1),
                SimulationModelTestFactory.CreateMove("Stealth Rock", 0.0, requestMoveIndex: 2,
                    statusEffect: StatusMoveEffect.StealthRock))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Chip Move", [0.1])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100,
            opponentBenchAliveCount: 5);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        Assert.That(members[0].Moves[action.MoveListIndex].StatusEffect,
            Is.EqualTo(StatusMoveEffect.StealthRock));
    }

    [Test]
    public void Test_FindBestAction_ShouldNotReSetStealthRock_WhenAlreadyUp()
    {
        // Arrange - rocks are already up, so setting them again just wastes the turn taking a hit
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 120,
                SimulationModelTestFactory.CreateMove("Weak Attack", 0.15, requestMoveIndex: 1),
                SimulationModelTestFactory.CreateMove("Stealth Rock", 0.0, requestMoveIndex: 2,
                    statusEffect: StatusMoveEffect.StealthRock))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Chip Move", [0.1])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100,
            opponentBenchAliveCount: 5,
            initialOpponentField: new OpponentFieldConditions { StealthRock = true });

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        Assert.That(members[0].Moves[action.MoveListIndex].StatusEffect,
            Is.EqualTo(StatusMoveEffect.None));
    }

    [Test]
    public void Test_FindBestAction_ShouldNotSetStealthRock_WhenALethalAttackIsAvailableAndOpponentThreatens()
    {
        // Arrange - a guaranteed knock-out beats setting rocks when delaying it would cost heavy damage
        // (against a non-threatening opponent, setting rocks first and KOing next turn is actually fine)
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 120,
                SimulationModelTestFactory.CreateMove("Knock Out", 1.0, requestMoveIndex: 1),
                SimulationModelTestFactory.CreateMove("Stealth Rock", 0.0, requestMoveIndex: 2,
                    statusEffect: StatusMoveEffect.StealthRock))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Strong Attack", [0.9])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100,
            opponentBenchAliveCount: 5);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        Assert.That(members[0].Moves[action.MoveListIndex].Name, Is.EqualTo("Knock Out"));
    }

    [Test]
    public void Test_FindBestAction_ShouldNotSetStealthRock_WhenOpponentIsDownToItsLastMon()
    {
        // Arrange - no benched opponents means hazards have nothing to chip
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 120,
                SimulationModelTestFactory.CreateMove("Weak Attack", 0.15, requestMoveIndex: 1),
                SimulationModelTestFactory.CreateMove("Stealth Rock", 0.0, requestMoveIndex: 2,
                    statusEffect: StatusMoveEffect.StealthRock))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Chip Move", [0.1])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100,
            opponentBenchAliveCount: 0);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        Assert.That(members[0].Moves[action.MoveListIndex].Name, Is.EqualTo("Weak Attack"));
    }

    [Test]
    public void Test_FindBestAction_ShouldTaunt_WhenOpponentIsPassive()
    {
        // Arrange - a passive opponent barely dents us, so denying its status plan with Taunt is best
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 120,
                SimulationModelTestFactory.CreateMove("Weak Attack", 0.1, requestMoveIndex: 1),
                SimulationModelTestFactory.CreateMove("Taunt", 0.0, requestMoveIndex: 2,
                    statusEffect: StatusMoveEffect.Taunt))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Weak Chip", [0.05])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100,
            opponentBenchAliveCount: 2, opponentIsPassive: true);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        Assert.That(members[0].Moves[action.MoveListIndex].StatusEffect, Is.EqualTo(StatusMoveEffect.Taunt));
    }

    [Test]
    public void Test_FindBestAction_ShouldNotReTaunt_WhenOpponentIsAlreadyTaunted()
    {
        // Arrange - the passive opponent is already taunted, so spending another turn on Taunt is wasted;
        // chipping it with the weak attack is strictly better
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 120,
                SimulationModelTestFactory.CreateMove("Weak Attack", 0.1, requestMoveIndex: 1),
                SimulationModelTestFactory.CreateMove("Taunt", 0.0, requestMoveIndex: 2,
                    statusEffect: StatusMoveEffect.Taunt))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Weak Chip", [0.05])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100,
            opponentBenchAliveCount: 2, opponentIsPassive: true,
            initialOpponentField: new OpponentFieldConditions { Taunted = true });

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        Assert.That(members[0].Moves[action.MoveListIndex].Name, Is.EqualTo("Weak Attack"));
    }

    [Test]
    public void Test_FindBestAction_ShouldNotTaunt_WhenOpponentIsThreatening()
    {
        // Arrange - a hard-hitting opponent is not passive, so Taunt has no value and wastes a turn
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 120,
                SimulationModelTestFactory.CreateMove("Solid Attack", 0.45, requestMoveIndex: 1),
                SimulationModelTestFactory.CreateMove("Taunt", 0.0, requestMoveIndex: 2,
                    statusEffect: StatusMoveEffect.Taunt))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Strong Attack", [0.5])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100,
            opponentBenchAliveCount: 2, opponentIsPassive: false);

        // Act
        var action = _minimaxSearch.FindBestAction(model);

        // Assert
        Assert.That(members[0].Moves[action.MoveListIndex].Name, Is.EqualTo("Solid Attack"));
    }
}
