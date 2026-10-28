using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Core.Services.Probabilities;
using System.Text.Json.Nodes;
using ElsaMina.Battles.Strategies.MiniPokeSearch;
using ElsaMina.UnitTests.Battles.Strategies.MiniPokeSearch;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPoke;

[TestFixture]
public class MiniPokeBattleDecisionServiceTest
{
    // The calc fallback picks a random move: make it pick a move index the model never maps to
    private const int FALLBACK_MOVE_CHOICE = 99;
    private const int ACTION_COUNT = BattleObservation.ActionCount;
    private static readonly MiniPokeModelOptions OPTIONS = new(BattleDecisionStrategy.MiniPokeV1_1M, "Models/MiniPokeV1_1M");
    private static readonly int[] SECOND_CHOICE = [2];
    private static readonly int[] THIRD_CHOICE = [3];
    private static readonly string[] GUARDED_MOVES = ["earthquake", "rockblast", "glare", "coil"];
    private static readonly int[] FOURTH_CHOICE = [4];
    private static readonly int[] FIFTH_CHOICE = [5];

    private IMiniPokeModel _model;
    private IMiniPokeEncoder _encoder;
    private IRandomService _randomService;
    private IShowdownSimulator _simulator;
    private MiniPokeBattleDecisionService _service;
    private MiniPokeBattleDecisionService _searchService;

    [SetUp]
    public void SetUp()
    {
        _model = Substitute.For<IMiniPokeModel>();
        _model.IsLoaded.Returns(true);
        _encoder = Substitute.For<IMiniPokeEncoder>();
        _randomService = Substitute.For<IRandomService>();
        _randomService.RandomElement(Arg.Any<IEnumerable<int>>()).Returns(FALLBACK_MOVE_CHOICE);
        var fallback = new CalcBasedBattleDecisionService(_randomService, Substitute.For<IOpponentMovesPredictor>(),
            Substitute.For<IBattleSearchAlgorithm>());
        _simulator = Substitute.For<IShowdownSimulator>();
        var guard = new MiniPokeActionGuard(TestBattleDex.Instance);
        var search = new MiniPokeTurnSearch(_simulator, new BattleMessageParser(TestBattleDex.Instance));
        _service = new MiniPokeBattleDecisionService(_model, _encoder, fallback, guard, search, OPTIONS);
        _searchService = new MiniPokeBattleDecisionService(_model, _encoder, fallback, guard, search,
            OPTIONS with { Strategy = BattleDecisionStrategy.MiniPokeV1_1M_Search, Search = true });
    }

    private static BattleContext CreateSinglesContext(int activeSlots = 1)
    {
        var moves = Enumerable.Range(1, 4)
            .Select(index => new BattleMoveState { Id = $"move{index}", Pp = 10, MaxPp = 10 })
            .ToList();
        return new BattleContext("battle-gen9randombattle-1")
        {
            SideId = "p1",
            ActiveSlots = Enumerable.Range(0, activeSlots).Select(_ => new BattleActiveSlot { Moves = moves }).ToList()
        };
    }

    /// <summary>
    /// An observation where every action is legal, team slot i is at request index 5 - i, and model move j is
    /// request move 3 - j.
    /// </summary>
    private static BattleObservation CreateObservation()
    {
        var observation = new BattleObservation(84, 39, 81);
        Array.Fill(observation.Mask, true);
        for (var slot = 0; slot < BattleObservation.SlotCount; slot++)
        {
            observation.TeamSlotToSideIndex[slot] = BattleObservation.SlotCount - 1 - slot;
        }

        for (var move = 0; move < BattleObservation.MoveCount; move++)
        {
            observation.MoveSlotToRequestIndex[move] = BattleObservation.MoveCount - 1 - move;
        }

        return observation;
    }

    private void ModelChooses(int action, BattleObservation observation = null)
    {
        observation ??= CreateObservation();
        _encoder.Encode(Arg.Any<BattleContext>()).Returns(observation);
        var logits = new float[ACTION_COUNT];
        logits[action] = 10f;
        _model.Evaluate(observation).Returns((logits, 0.5f));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldSwitchToRequestIndex_WhenModelChoosesSwitch()
    {
        // Arrange: team slot 1 is the 5th Pokémon of the current request
        ModelChooses(1);

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Switch));
            Assert.That(decision.Choices, Is.EqualTo(FIFTH_CHOICE));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldUseRequestMoveIndex_WhenModelChoosesMove()
    {
        // Arrange: model move 0 is the request's 4th move
        ModelChooses(6);

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            Assert.That(decision.Choices, Is.EqualTo(FOURTH_CHOICE));
            Assert.That(decision.UseTerastallize, Is.False);
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldTerastallize_WhenModelChoosesTeraMove()
    {
        // Arrange: action 12 is terastallize + model move 2, the request's 2nd move
        ModelChooses(12);

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            Assert.That(decision.Choices, Is.EqualTo(SECOND_CHOICE));
            Assert.That(decision.UseTerastallize, Is.True);
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldChooseBestLegalAction_WhenBetterActionIsMasked()
    {
        // Arrange
        var observation = CreateObservation();
        Array.Fill(observation.Mask, false);
        observation.Mask[7] = true;
        observation.Mask[8] = true;
        _encoder.Encode(Arg.Any<BattleContext>()).Returns(observation);
        var logits = new float[ACTION_COUNT];
        logits[0] = 50f;
        logits[7] = 1f;
        logits[8] = 2f;
        _model.Evaluate(observation).Returns((logits, 0f));

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert: action 8 is model move 2, the request's 2nd move
        Assert.That(decision.Choices, Is.EqualTo(SECOND_CHOICE));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPlayNextBestAction_WhenGuardVetoesModelChoice()
    {
        // Arrange: model move 0, Earthquake, already had no effect on Eelektross
        var context = CreateGuardedContext();
        var observation = CreateGuardedObservation();
        _encoder.Encode(Arg.Any<BattleContext>()).Returns(observation);
        var logits = new float[ACTION_COUNT];
        logits[6] = 10f;
        logits[10] = 9f;
        logits[7] = 5f;
        _model.Evaluate(observation).Returns((logits, 0f));

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert: action 7 is model move 1, the request's 3rd move
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            Assert.That(decision.Choices, Is.EqualTo(THIRD_CHOICE));
            Assert.That(decision.UseTerastallize, Is.False);
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldKeepModelChoice_WhenGuardVetoesEveryLegalAction()
    {
        // Arrange
        var context = CreateGuardedContext();
        var observation = CreateGuardedObservation();
        Array.Fill(observation.Mask, false);
        observation.Mask[6] = true;
        _encoder.Encode(Arg.Any<BattleContext>()).Returns(observation);
        var logits = new float[ACTION_COUNT];
        logits[6] = 10f;
        _model.Evaluate(observation).Returns((logits, 0f));

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert: action 6 is model move 0, the request's 4th move
        Assert.That(decision.Choices, Is.EqualTo(FOURTH_CHOICE));
    }

    private static BattleContext CreateGuardedContext()
    {
        var context = CreateSinglesContext();
        context.Lines.AddRange([
            "|switch|p1a: Sandaconda|Sandaconda, L84|258/258", "|switch|p2a: Eelektross|Eelektross, L87|100/100",
            "|turn|1", "|move|p1a: Sandaconda|Earthquake|p2a: Eelektross",
            "|-immune|p2a: Eelektross|[from] ability: Levitate", "|turn|2"
        ]);
        return context;
    }

    private static BattleObservation CreateGuardedObservation()
    {
        var observation = CreateObservation();
        GUARDED_MOVES.CopyTo(observation.MoveSlotIds, 0);
        return observation;
    }

    private void SimulatorPrefersMoveOne()
    {
        _simulator.IsAvailable.Returns(true);
        var reply = new SimulationReply(
        [
            new SimulationDeterminization(["coil"],
                [
                    [[new SimulationLeaf(["|win|Them"], "p2")]], [[new SimulationLeaf(["|win|Elsa"], "p1")]],
                    [[new SimulationLeaf(["|win|Them"], "p2")]]
                ])
        ], null);
        _simulator.SimulateAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>()).Returns(reply);
    }

    private void ModelPrefersMoveZero()
    {
        var observation = CreateGuardedObservation();
        _encoder.Encode(Arg.Any<BattleContext>()).Returns(observation);
        var logits = Enumerable.Repeat(-10f, ACTION_COUNT).ToArray();
        logits[6] = 5f;
        logits[7] = 4f;
        _model.Evaluate(observation).Returns((logits, 0f));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldNotSearch_WhenTheStrategyIsNotASearchVariant()
    {
        // Arrange
        ModelPrefersMoveZero();
        SimulatorPrefersMoveOne();

        // Act
        var decision = await _service.GetDecisionAsync(new SearchTestBattle().Context);

        // Assert: action 6 is model move 0, the request's 4th move
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Choices, Is.EqualTo(FOURTH_CHOICE));
            Assert.That(_service.CanSearch, Is.False);
            await _simulator.DidNotReceive().SimulateAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPlayTheSearchedAction_WhenTheSimulatorIsAvailable()
    {
        // Arrange: the model prefers move 0, but only move 1's simulated turn wins
        ModelPrefersMoveZero();
        SimulatorPrefersMoveOne();

        // Act
        var decision = await _searchService.GetDecisionAsync(new SearchTestBattle().Context);

        // Assert: action 7 is model move 1, the request's 3rd move
        Assert.That(decision.Choices, Is.EqualTo(THIRD_CHOICE));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPlayTheModelsAction_WhenTheSearchFails()
    {
        // Arrange
        ModelPrefersMoveZero();
        _simulator.IsAvailable.Returns(true);
        _simulator.SimulateAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("simulator crashed"));

        // Act
        var decision = await _searchService.GetDecisionAsync(new SearchTestBattle().Context);

        // Assert: action 6 is model move 0, the request's 4th move
        Assert.That(decision.Choices, Is.EqualTo(FOURTH_CHOICE));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallBack_WhenModelIsNotLoaded()
    {
        // Arrange
        _model.IsLoaded.Returns(false);

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Choices, Is.EqualTo(new[] { FALLBACK_MOVE_CHOICE }));
            _encoder.DidNotReceive().Encode(Arg.Any<BattleContext>());
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallBack_WhenBattleIsDoubles()
    {
        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext(activeSlots: 2));

        // Assert: the calc strategy picks one move per active slot
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            Assert.That(decision.Choices, Has.Count.EqualTo(2));
            _encoder.DidNotReceive().Encode(Arg.Any<BattleContext>());
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallBack_WhenInTeamPreview()
    {
        // Arrange
        var context = CreateSinglesContext();
        context.TeamPreview = true;
        context.SidePokemon = [new BattlePokemonState { Ident = "p1: Garchomp" }];
        _randomService.NextInt(Arg.Any<int>(), Arg.Any<int>()).Returns(1);

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.TeamPreview));
            _encoder.DidNotReceive().Encode(Arg.Any<BattleContext>());
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallBack_WhenSwitchActionCannotBeMapped()
    {
        // Arrange: the chosen team slot isn't in the current request
        var observation = CreateObservation();
        observation.TeamSlotToSideIndex[2] = -1;
        ModelChooses(2, observation);

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Choices, Is.EqualTo(new[] { FALLBACK_MOVE_CHOICE }));
            _model.Received(1).Evaluate(observation);
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallBack_WhenMoveActionCannotBeMapped()
    {
        // Arrange
        var observation = CreateObservation();
        observation.MoveSlotToRequestIndex[3] = -1;
        ModelChooses(9, observation);

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert
        Assert.That(decision.Choices, Is.EqualTo(new[] { FALLBACK_MOVE_CHOICE }));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallBack_WhenStateCannotBeEncoded()
    {
        // Arrange
        _encoder.Encode(Arg.Any<BattleContext>()).Returns((BattleObservation)null);

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Choices, Is.EqualTo(new[] { FALLBACK_MOVE_CHOICE }));
            _model.DidNotReceive().Evaluate(Arg.Any<BattleObservation>());
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallBack_WhenNoActionIsLegal()
    {
        // Arrange
        var observation = CreateObservation();
        Array.Fill(observation.Mask, false);
        _encoder.Encode(Arg.Any<BattleContext>()).Returns(observation);

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Choices, Is.EqualTo(new[] { FALLBACK_MOVE_CHOICE }));
            _model.DidNotReceive().Evaluate(Arg.Any<BattleObservation>());
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallBack_WhenModelThrows()
    {
        // Arrange
        _encoder.Encode(Arg.Any<BattleContext>()).Returns(CreateObservation());
        _model.Evaluate(Arg.Any<BattleObservation>()).Throws(new InvalidOperationException("onnx failure"));

        // Act
        var decision = await _service.GetDecisionAsync(CreateSinglesContext());

        // Assert
        Assert.That(decision.Choices, Is.EqualTo(new[] { FALLBACK_MOVE_CHOICE }));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenWaitingForOpponent()
    {
        // Arrange
        var context = CreateSinglesContext();
        context.Wait = true;

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.Null);
            _encoder.DidNotReceive().Encode(Arg.Any<BattleContext>());
        }
    }

    [Test]
    public void Test_Strategy_ShouldBeTheModelVersionStrategy()
    {
        // Assert
        Assert.That(_service.Strategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV1_1M));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenBattleIsOver()
    {
        // Arrange
        var context = CreateSinglesContext();
        context.IsBattleOver = true;

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Null);
    }
}
