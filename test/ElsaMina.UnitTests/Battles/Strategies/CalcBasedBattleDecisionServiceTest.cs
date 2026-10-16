using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Battles.Strategies.Simulation;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Battles.Strategies;

public class CalcBasedBattleDecisionServiceTest
{
    private IRandomService _randomService;
    private IOpponentMovesPredictor _opponentMovesPredictor;
    private IBattleSearchAlgorithm _searchAlgorithm;
    private CalcBasedBattleDecisionService _service;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>();
        _randomService.RandomElement(Arg.Any<IEnumerable<int>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<int>>().First());
        _opponentMovesPredictor = Substitute.For<IOpponentMovesPredictor>();
        _opponentMovesPredictor.PredictAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new OpponentPrediction([new PredictedMove("Waterfall", 0.9)], null));
        _searchAlgorithm = Substitute.For<IBattleSearchAlgorithm>();
        _service = new CalcBasedBattleDecisionService(_randomService, _opponentMovesPredictor, _searchAlgorithm);
    }

    private static BattlePokemonState CreatePikachu(bool isActive = true, bool isFainted = false)
    {
        return new BattlePokemonState
        {
            Ident = "p1: Pikachu",
            Details = "Pikachu, L50, M",
            Condition = isFainted ? "0 fnt" : "110/110",
            CurrentHp = isFainted ? 0 : 110,
            MaxHp = 110,
            IsActive = isActive,
            IsFainted = isFainted,
            Stats = new BattlePokemonStats(75, 60, 70, 70, 110),
            Moves = ["thunderbolt", "surf"]
        };
    }

    private static BattlePokemonState CreateSnorlax()
    {
        return new BattlePokemonState
        {
            Ident = "p1: Snorlax",
            Details = "Snorlax, L50, M",
            Condition = "200/220",
            CurrentHp = 200,
            MaxHp = 220,
            Stats = new BattlePokemonStats(130, 85, 85, 130, 50),
            Moves = ["bodyslam", "rest"]
        };
    }

    private static BattlePokemonState CreateCharizard()
    {
        return new BattlePokemonState
        {
            Ident = "p1: Charizard",
            Details = "Charizard, L50, M",
            Condition = "150/150",
            CurrentHp = 150,
            MaxHp = 150,
            Stats = new BattlePokemonStats(84, 78, 109, 85, 100),
            Moves = ["flamethrower", "airslash"]
        };
    }

    private static BattleActiveSlot CreatePikachuSlot(string canTerastallize = "")
    {
        return new BattleActiveSlot
        {
            CanTerastallize = canTerastallize,
            Moves =
            [
                new BattleMoveState { Name = "Surf", Id = "surf", Pp = 15, MaxPp = 15 },
                new BattleMoveState { Name = "Thunderbolt", Id = "thunderbolt", Pp = 15, MaxPp = 15 }
            ]
        };
    }

    private static OpponentPokemonState CreateGyarados(string lastUsedMove = "")
    {
        return new OpponentPokemonState
        {
            Species = "Gyarados",
            Level = 50,
            HpPercent = 100,
            IsActive = true,
            LastUsedMove = lastUsedMove
        };
    }

    private static BattleContext CreateSingleBattleContext()
    {
        return new BattleContext("battle-gen9ou-123456")
        {
            SideId = "p1",
            SidePokemon = [CreatePikachu(), CreateSnorlax()],
            ActiveSlots = [CreatePikachuSlot()],
            OpponentPokemon = [CreateGyarados()]
        };
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenBattleIsOver()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.IsBattleOver = true;

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenWaitingForOpponent()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.Wait = true;

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickRandomLead_WhenTeamPreview()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.TeamPreview = true;
        _randomService.NextInt(1, 3).Returns(2);

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.TeamPreview));
            Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.TWO));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenNoActiveSlot()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.ActiveSlots = [];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPlaySearchedMove_WhenSearchPicksMove()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        _searchAlgorithm.FindBestAction(Arg.Any<SimulationModel>()).Returns(callInfo =>
        {
            var model = callInfo.Arg<SimulationModel>();
            var moves = model.Members[model.ActiveMemberIndex].Moves;
            var thunderboltIndex = moves.FindIndex(move => move.Name == "Thunderbolt");
            return new SimulationAction(SimulationActionKind.Move, model.ActiveMemberIndex, thunderboltIndex, true);
        });

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.TWO));
            Assert.That(decision.UseTerastallize, Is.True);
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldSwitch_WhenSearchPicksSwitch()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        _searchAlgorithm.FindBestAction(Arg.Any<SimulationModel>()).Returns(callInfo =>
        {
            var model = callInfo.Arg<SimulationModel>();
            var snorlaxIndex = model.Members.FindIndex(member => member.TeamSlot == 2);
            return new SimulationAction(SimulationActionKind.Switch, snorlaxIndex);
        });

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Switch));
            Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.TWO));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPlayRandomMove_WhenSearchFindsNothing()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        _searchAlgorithm.FindBestAction(Arg.Any<SimulationModel>()).Returns((SimulationAction)null);

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.ONE));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPlayRandomMove_WhenSearchedMoveHasNoRequestIndex()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        _searchAlgorithm.FindBestAction(Arg.Any<SimulationModel>()).Returns(callInfo =>
        {
            var model = callInfo.Arg<SimulationModel>();
            var moves = model.Members[model.ActiveMemberIndex].Moves;
            moves[0] = new SimulationMove { Name = moves[0].Name, RequestMoveIndex = 0 };
            return new SimulationAction(SimulationActionKind.Move, model.ActiveMemberIndex);
        });
        context.ActiveSlots[0].Moves[0] = new BattleMoveState { Name = "Surf", Pp = 0, MaxPp = 15 };

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert - Surf is out of PP so the fallback picks Thunderbolt
        Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.TWO));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldNotSearch_WhenNoActiveOpponent()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.OpponentPokemon = [];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            _searchAlgorithm.DidNotReceive().FindBestAction(Arg.Any<SimulationModel>());
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPlayRandomMove_WhenPredictionFails()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        _opponentMovesPredictor.PredictAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("down"));

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            _searchAlgorithm.DidNotReceive().FindBestAction(Arg.Any<SimulationModel>());
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenFallbackHasNoUsableMove()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.OpponentPokemon = [];
        context.ActiveSlots =
        [
            new BattleActiveSlot
            {
                Moves = [new BattleMoveState { Name = "Surf", Pp = 15, MaxPp = 15, IsDisabled = true }]
            }
        ];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldKeepRechargeAvailable_WhenRechargeHasNoPp()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.OpponentPokemon = [];
        context.ActiveSlots =
        [
            new BattleActiveSlot
            {
                Moves = [new BattleMoveState { Name = "Recharge", Pp = 0, MaxPp = 1, IsDisabled = true }]
            }
        ];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.ONE));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldSwitchToSearchedPokemon_WhenForcedToSwitch()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.SidePokemon = [CreatePikachu(isActive: false, isFainted: true), CreateSnorlax(), CreateCharizard()];
        context.ForceSwitchSlots = [true];
        _searchAlgorithm.FindBestAction(Arg.Any<SimulationModel>()).Returns(callInfo =>
        {
            var model = callInfo.Arg<SimulationModel>();
            var charizardIndex = model.Members.FindIndex(member => member.TeamSlot == 3);
            return new SimulationAction(SimulationActionKind.Switch, charizardIndex);
        });

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Switch));
            Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.THREE));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickRandomSwitchIn_WhenSearchFailsAndNoMoveSeen()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.SidePokemon = [CreatePikachu(isActive: false, isFainted: true), CreateCharizard(), CreateSnorlax()];
        context.ForceSwitchSlots = [true];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Switch));
            Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.TWO));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickSwitchInTakingLeastDamage_WhenOpponentMoveIsKnown()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.SidePokemon = [CreatePikachu(isActive: false, isFainted: true), CreateCharizard(), CreateSnorlax()];
        context.OpponentPokemon = [CreateGyarados(lastUsedMove: "Waterfall")];
        context.ForceSwitchSlots = [true];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert - Charizard is weak to Waterfall, Snorlax takes it neutrally with more HP
        Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.THREE));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickDistinctSwitchIns_WhenSeveralSlotsMustSwitch()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.SidePokemon = [CreatePikachu(isActive: false, isFainted: true), CreateCharizard(), CreateSnorlax()];
        context.ForceSwitchSlots = [true, true];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.TWO_AND_THREE));
            _searchAlgorithm.DidNotReceive().FindBestAction(Arg.Any<SimulationModel>());
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenNoPokemonCanSwitchIn()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.SidePokemon = [CreatePikachu(isActive: false, isFainted: true)];
        context.ForceSwitchSlots = [true];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickStrongestMovePerSlot_WhenDoubleBattle()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.ActiveSlots = [CreatePikachuSlot(), CreatePikachuSlot()];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert - Thunderbolt is super effective on Gyarados, Surf is resisted
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.TWO_AND_TWO));
            Assert.That(decision.UseTerastallize, Is.False);
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldTerastallize_WhenTeraImprovesDamageInDoubleBattle()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.ActiveSlots = [CreatePikachuSlot("Electric"), CreatePikachuSlot()];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.TWO_AND_TWO));
            Assert.That(decision.UseTerastallize, Is.True);
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickRandomMoves_WhenDoubleBattleHasNoKnownOpponent()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.OpponentPokemon = [];
        context.ActiveSlots = [CreatePikachuSlot(), CreatePikachuSlot()];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision.Choices, Is.EqualTo(ExpectedChoices.ONE_AND_ONE));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenDoubleBattleSlotHasNoUsableMove()
    {
        // Arrange
        var context = CreateSingleBattleContext();
        context.ActiveSlots =
        [
            CreatePikachuSlot(),
            new BattleActiveSlot { Moves = [new BattleMoveState { Name = "Surf", Pp = 0, MaxPp = 15 }] }
        ];

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Null);
    }
}
