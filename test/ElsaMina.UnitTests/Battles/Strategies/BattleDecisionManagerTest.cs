using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Gladion;
using ElsaMina.Battles.Strategies.GladionSearch;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Battles.Tracking;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Strategies;

[TestFixture]
public class BattleDecisionManagerTest
{
    private static readonly BattleDecisionStrategy[] REGISTERED_STRATEGIES =
    [
        BattleDecisionStrategy.DamageCalc, BattleDecisionStrategy.GladionV1_22M,
        BattleDecisionStrategy.GladionV1_22M_Search
    ];

    private CalcBasedBattleDecisionService _calcService;
    private GladionActionGuard _guard;
    private GladionTurnSearch _search;
    private GladionBattleDecisionService _gladionV1Service;
    private GladionBattleDecisionService _gladionV1SearchService;
    private IShowdownSimulator _simulator;
    private BattleDecisionManager _manager;

    [SetUp]
    public void SetUp()
    {
        var opponentMovesPredictor = Substitute.For<IOpponentMovesPredictor>();
        var randomService = Substitute.For<IRandomService>();
        var searchAlgorithm = Substitute.For<IBattleSearchAlgorithm>();

        _calcService = new CalcBasedBattleDecisionService(randomService, opponentMovesPredictor, searchAlgorithm);
        _guard = new GladionActionGuard(Substitute.For<IBattleDex>());
        _simulator = Substitute.For<IShowdownSimulator>();
        _simulator.IsAvailable.Returns(true);
        _search = new GladionTurnSearch(_simulator, Substitute.For<IBattleMessageParser>());

        // As BattlesModule registers them: the model alone and with search
        _gladionV1Service = Service(BattleDecisionStrategy.GladionV1_22M, "GladionV1_22M");
        _gladionV1SearchService = Service(BattleDecisionStrategy.GladionV1_22M_Search, "GladionV1_22M");
        _manager = new BattleDecisionManager(_calcService, [_gladionV1Service, _gladionV1SearchService]);
    }

    private GladionBattleDecisionService Service(BattleDecisionStrategy strategy, string model) =>
        new(Substitute.For<IGladionModel>(), Substitute.For<IGladionEncoder>(), _calcService, _guard, _search,
            new GladionModelOptions(strategy, $"Models/{model}", strategy.IsSearch()));

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToTheSearchVariant_WhenInputNamesIt()
    {
        // Arrange
        _manager.ActiveStrategy = BattleDecisionStrategy.DamageCalc;

        // Act
        var result = _manager.TrySetStrategy("gladionv1-22m+search", out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.GladionV1_22M_Search));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.GladionV1_22M_Search));
            Assert.That(((GladionBattleDecisionService)_manager.GetCurrentService()).CanSearch, Is.True);
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldRefuseTheSearchVariants_WhenTheSimulatorIsMissing()
    {
        // Arrange
        _simulator.IsAvailable.Returns(false);

        // Act
        var result = _manager.TrySetStrategy("gladionv1-22m+search", out _);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(_manager.AvailableStrategies.Where(strategy => strategy.IsSearch()), Is.Empty);
            Assert.That(_manager.AvailableStrategies, Does.Contain(BattleDecisionStrategy.GladionV1_22M));
        }
    }

    [Test]
    public void Test_ActiveStrategy_ShouldDefaultToGladionV1WithSearch()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.GladionV1_22M_Search));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_gladionV1SearchService));
        }
    }

    [Test]
    public void Test_GetCurrentService_ShouldReturnCalcService_WhenActiveGladionModelIsNotRegistered()
    {
        // Arrange
        var manager = new BattleDecisionManager(_calcService, []);

        // Assert
        Assert.That(manager.GetCurrentService(), Is.SameAs(_calcService));
    }

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToDamageCalc_WhenInputIsCalc()
    {
        // Act
        var result = _manager.TrySetStrategy("calc", out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_calcService));
        }
    }

    [TestCase("gladionv1-22m")]
    [TestCase(" GladionV1-22M ")]
    [TestCase("gladion")]
    public void Test_TrySetStrategy_ShouldSwitchToGladionV1_WhenInputNamesItOrAsksForTheLatest(string input)
    {
        // Act
        var result = _manager.TrySetStrategy(input, out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.GladionV1_22M));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.GladionV1_22M));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_gladionV1Service));
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldReturnFalse_WhenGladionVersionIsNotRegistered()
    {
        // Arrange
        var manager = new BattleDecisionManager(_calcService, []);

        // Act
        var result = manager.TrySetStrategy("gladionv1-22m", out _);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(manager.AvailableStrategies, Does.Not.Contain(BattleDecisionStrategy.GladionV1_22M));
            Assert.That(manager.TrySetStrategy("gladion", out _), Is.False);
        }
    }

    [Test]
    public void Test_AvailableStrategies_ShouldListEveryRegisteredStrategy()
    {
        // Assert
        Assert.That(_manager.AvailableStrategies, Is.EqualTo(REGISTERED_STRATEGIES));
    }

    [Test]
    public void Test_TrySetStrategy_ShouldReturnFalse_WhenInputIsInvalid()
    {
        // Act
        var result = _manager.TrySetStrategy("nonexistent_strategy", out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.GladionV1_22M_Search));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.GladionV1_22M_Search));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldDelegateToActiveService()
    {
        // Arrange
        _manager.ActiveStrategy = BattleDecisionStrategy.DamageCalc;
        var context = new BattleContext("battle-gen9ou-1") { IsBattleOver = true };

        // Act
        var decision = await _manager.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Null);
    }
}
