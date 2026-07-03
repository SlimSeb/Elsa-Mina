using ElsaMina.Battles;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Simulation;

namespace ElsaMina.UnitTests.Battles.Strategies.Simulation;

public class SimulationModelBuilderTest
{
    private static BattleContext CreateContext()
    {
        var context = new BattleContext("battle-gen9ou-123456");
        context.SidePokemon =
        [
            new BattlePokemonState
            {
                Ident = "p1: Pikachu",
                Details = "Pikachu, L50, M",
                Condition = "110/110",
                CurrentHp = 110,
                MaxHp = 110,
                IsActive = true,
                Stats = new BattlePokemonStats(75, 60, 70, 70, 110),
                Moves = ["thunderbolt", "surf"]
            },
            new BattlePokemonState
            {
                Ident = "p1: Snorlax",
                Details = "Snorlax, L50, M",
                Condition = "200/220",
                CurrentHp = 200,
                MaxHp = 220,
                Stats = new BattlePokemonStats(130, 85, 85, 130, 50),
                Moves = ["bodyslam", "rest"]
            }
        ];
        context.ActiveSlots =
        [
            new BattleActiveSlot
            {
                Moves =
                [
                    new BattleMoveState { Name = "Thunderbolt", Id = "thunderbolt", Pp = 15, MaxPp = 15 },
                    new BattleMoveState { Name = "Surf", Id = "surf", Pp = 15, MaxPp = 15 }
                ]
            }
        ];
        context.OpponentPokemon =
        [
            new OpponentPokemonState
            {
                Species = "Gyarados",
                Level = 50,
                HpPercent = 100,
                IsActive = true
            }
        ];
        return context;
    }

    [Test]
    public void Test_TryBuild_ShouldComputeDamageMatrices_WhenBattleStateIsComplete()
    {
        // Arrange
        var context = CreateContext();
        var predictedMoves = new List<PredictedMove>
        {
            new("Waterfall", 0.8),
            new("Dragon Dance", 0.6)
        };

        // Act
        var model = SimulationModelBuilder.TryBuild(context, predictedMoves, forcedSwitch: false);

        // Assert
        Assert.That(model, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Members, Has.Count.EqualTo(2));
            Assert.That(model.ActiveMemberIndex, Is.EqualTo(0));

            var thunderbolt = model.Members[0].Moves.Single(move => move.Name == "Thunderbolt");
            Assert.That(thunderbolt.RequestMoveIndex, Is.EqualTo(1));
            Assert.That(thunderbolt.DamageRatio, Is.GreaterThan(0.5),
                "Thunderbolt should deal heavy damage to Gyarados (4x weakness)");

            // Status moves are filtered out of the opponent's simulated options
            Assert.That(model.OpponentMoves, Has.Count.EqualTo(1));
            Assert.That(model.OpponentMoves[0].Name, Is.EqualTo("Waterfall"));
            Assert.That(model.OpponentMoves[0].DamageToMembers[0], Is.GreaterThan(0.0));
        }
    }

    [Test]
    public void Test_TryBuild_ShouldExcludeActivePokemon_WhenBuildingForForcedSwitch()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var model = SimulationModelBuilder.TryBuild(context, [], forcedSwitch: true);

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.ActiveMemberIndex, Is.EqualTo(-1));
    }

    [Test]
    public void Test_TryBuild_ShouldReturnNull_WhenThereIsNoActiveOpponent()
    {
        // Arrange
        var context = CreateContext();
        context.OpponentPokemon[0].IsActive = false;

        // Act & Assert
        Assert.That(SimulationModelBuilder.TryBuild(context, [], forcedSwitch: false), Is.Null);
    }
}
