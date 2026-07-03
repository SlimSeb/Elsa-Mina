using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Battles.Strategies.Simulation;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.UnitTests.Battles.Strategies.Simulation;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Strategies.Search;

public class MonteCarloTreeSearchTest
{
    private IRandomService _randomService;

    private MonteCarloTreeSearch _monteCarloTreeSearch;

    [SetUp]
    public void SetUp()
    {
        var random = new Random(42);
        _randomService = Substitute.For<IRandomService>();
        _randomService.NextInt(Arg.Any<int>()).Returns(callInfo => random.Next(callInfo.Arg<int>()));
        _randomService.RandomElement(Arg.Any<IEnumerable<SimulationAction>>())
            .Returns(callInfo =>
            {
                var elements = callInfo.Arg<IEnumerable<SimulationAction>>().ToList();
                return elements[random.Next(elements.Count)];
            });

        _monteCarloTreeSearch = new MonteCarloTreeSearch(_randomService);
    }

    [Test]
    public void Test_FindBestAction_ShouldPickTheKnockOutMove_WhenAvailable()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 200,
                SimulationModelTestFactory.CreateMove("Weak Move", 0.2, requestMoveIndex: 1),
                SimulationModelTestFactory.CreateMove("Knock Out Move", 1.0, requestMoveIndex: 2))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Counter Move", [0.4])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);

        // Act
        var action = _monteCarloTreeSearch.FindBestAction(model);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(action.Kind, Is.EqualTo(SimulationActionKind.Move));
            Assert.That(members[0].Moves[action.MoveListIndex].Name, Is.EqualTo("Knock Out Move"));
        }
    }

    [Test]
    public void Test_FindBestAction_ShouldSwitch_WhenActivePokemonIsDoomed()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 0.5, speed: 50,
                SimulationModelTestFactory.CreateMove("Weak Move", 0.1)),
            SimulationModelTestFactory.CreateMember(2, 1.0, speed: 150,
                SimulationModelTestFactory.CreateMove("Strong Move", 0.6))
        };
        var opponentMoves = new List<OpponentSimulationMove>
        {
            SimulationModelTestFactory.CreateOpponentMove("Threatening Move", [0.6, 0.05])
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, opponentMoves, opponentSpeed: 100);

        // Act
        var action = _monteCarloTreeSearch.FindBestAction(model);

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
        Assert.That(_monteCarloTreeSearch.FindBestAction(model), Is.Null);
    }

    [Test]
    public void Test_FindBestAction_ShouldReturnTheOnlyAction_WhenSingleActionIsAvailable()
    {
        // Arrange
        var members = new List<SimulationTeamMember>
        {
            SimulationModelTestFactory.CreateMember(1, 1.0, speed: 100,
                SimulationModelTestFactory.CreateMove("Only Move", 0.3))
        };
        var model = SimulationModelTestFactory.CreateModel(0, members, [], opponentSpeed: 100,
            activeIsTrapped: true);

        // Act
        var action = _monteCarloTreeSearch.FindBestAction(model);

        // Assert
        Assert.That(action.Kind, Is.EqualTo(SimulationActionKind.Move));
    }
}
