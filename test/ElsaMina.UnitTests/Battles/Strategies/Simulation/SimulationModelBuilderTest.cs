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
        var prediction = new OpponentPrediction(
        [
            new PredictedMove("Waterfall", 0.8),
            new PredictedMove("Dragon Dance", 0.6)
        ], Spread: null);

        // Act
        var model = SimulationModelBuilder.TryBuild(context, prediction, forcedSwitch: false);

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
        var model = SimulationModelBuilder.TryBuild(context, OpponentPrediction.Empty, forcedSwitch: true);

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.ActiveMemberIndex, Is.EqualTo(-1));
    }

    [Test]
    public void Test_TryBuild_ShouldClassifyHazardAndTauntMoves()
    {
        // Arrange
        var context = CreateContext();
        context.ActiveSlots[0].Moves.Add(new BattleMoveState
            { Name = "Stealth Rock", Id = "stealthrock", Pp = 20, MaxPp = 20 });
        context.ActiveSlots[0].Moves.Add(new BattleMoveState
            { Name = "Taunt", Id = "taunt", Pp = 20, MaxPp = 20 });

        // Act
        var model = SimulationModelBuilder.TryBuild(context, OpponentPrediction.Empty, forcedSwitch: false);

        // Assert
        var active = model.Members[model.ActiveMemberIndex];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(active.Moves.Single(move => move.Name == "Stealth Rock").StatusEffect,
                Is.EqualTo(StatusMoveEffect.StealthRock));
            Assert.That(active.Moves.Single(move => move.Name == "Taunt").StatusEffect,
                Is.EqualTo(StatusMoveEffect.Taunt));
            Assert.That(active.Moves.Single(move => move.Name == "Thunderbolt").StatusEffect,
                Is.EqualTo(StatusMoveEffect.None));
        }
    }

    [Test]
    public void Test_TryBuild_ShouldSeedInitialOpponentHazards_FromContext()
    {
        // Arrange
        var context = CreateContext();
        context.OpponentSideStealthRock = true;
        context.OpponentSideSpikesLayers = 2;

        // Act
        var model = SimulationModelBuilder.TryBuild(context, OpponentPrediction.Empty, forcedSwitch: false);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.InitialOpponentField.StealthRock, Is.True);
            Assert.That(model.InitialOpponentField.SpikesLayers, Is.EqualTo(2));
        }
    }

    [Test]
    public void Test_TryBuild_ShouldMarkOpponentPassive_WhenItHasOnlyStatusMoves()
    {
        // Arrange - a set of purely status moves produces no damaging opponent moves
        var context = CreateContext();
        var prediction = new OpponentPrediction(
            [new PredictedMove("Thunder Wave", 0.9), new PredictedMove("Toxic", 0.8)], Spread: null);

        // Act
        var model = SimulationModelBuilder.TryBuild(context, prediction, forcedSwitch: false);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.OpponentMoves, Is.Empty);
            Assert.That(model.OpponentIsPassive, Is.True);
        }
    }

    [Test]
    public void Test_TryBuild_ShouldNotMarkOpponentPassive_WhenItHitsHard()
    {
        // Arrange - Waterfall from Gyarados is a heavy hit on Pikachu
        var context = CreateContext();
        var prediction = new OpponentPrediction([new PredictedMove("Waterfall", 0.9)], Spread: null);

        // Act
        var model = SimulationModelBuilder.TryBuild(context, prediction, forcedSwitch: false);

        // Assert
        Assert.That(model.OpponentIsPassive, Is.False);
    }

    [Test]
    public void Test_TryBuild_ShouldReturnNull_WhenThereIsNoActiveOpponent()
    {
        // Arrange
        var context = CreateContext();
        context.OpponentPokemon[0].IsActive = false;

        // Act & Assert
        Assert.That(SimulationModelBuilder.TryBuild(context, OpponentPrediction.Empty, forcedSwitch: false),
            Is.Null);
    }

    [Test]
    public void Test_TryBuild_ShouldComputeStealthRockChip_ForGroundedMembers()
    {
        // Arrange - Snorlax (Normal) takes a neutral Stealth Rock hit: 12.5% of max HP
        var context = CreateContext();
        context.OwnSideStealthRock = true;

        // Act
        var model = SimulationModelBuilder.TryBuild(context, OpponentPrediction.Empty, forcedSwitch: false);

        // Assert
        var snorlax = model.Members.Single(member => member.Species == "Snorlax");
        Assert.That(snorlax.SwitchInChipRatio, Is.EqualTo(0.125).Within(1e-9));
    }

    [Test]
    public void Test_TryBuild_ShouldAddSpikesChipOnTopOfRock_ForGroundedMembers()
    {
        // Arrange
        var context = CreateContext();
        context.OwnSideStealthRock = true;
        context.OwnSideSpikesLayers = 1;

        // Act
        var model = SimulationModelBuilder.TryBuild(context, OpponentPrediction.Empty, forcedSwitch: false);

        // Assert - 12.5% Stealth Rock + 12.5% one layer of Spikes
        var snorlax = model.Members.Single(member => member.Species == "Snorlax");
        Assert.That(snorlax.SwitchInChipRatio, Is.EqualTo(0.25).Within(1e-9));
    }

    [Test]
    public void Test_TryBuild_ShouldNotChipMembers_WhenNoHazardsAreSet()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var model = SimulationModelBuilder.TryBuild(context, OpponentPrediction.Empty, forcedSwitch: false);

        // Assert
        Assert.That(model.Members.All(member => member.SwitchInChipRatio == 0.0), Is.True);
    }

    [Test]
    public void Test_TryBuild_ShouldApplyPredictedSpread_MakingOpponentSpeedRealistic()
    {
        // Arrange - a max-speed spread must produce a higher opponent speed tier than the
        // calc's default 0-EV neutral spread, otherwise the bot mis-resolves turn order
        var context = CreateContext();
        var neutralModel = SimulationModelBuilder.TryBuild(context, OpponentPrediction.Empty,
            forcedSwitch: false);

        var maxSpeedPrediction = new OpponentPrediction([],
            new PredictedSpread("Jolly", HpEvs: 0, AtkEvs: 252, DefEvs: 0, SpaEvs: 0, SpdEvs: 4, SpeEvs: 252));

        // Act
        var investedModel = SimulationModelBuilder.TryBuild(context, maxSpeedPrediction, forcedSwitch: false);

        // Assert
        Assert.That(investedModel.OpponentSpeed, Is.GreaterThan(neutralModel.OpponentSpeed));
    }
}
