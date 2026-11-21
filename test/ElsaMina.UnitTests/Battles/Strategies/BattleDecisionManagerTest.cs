using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeSearch;
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
        BattleDecisionStrategy.DamageCalc, BattleDecisionStrategy.MiniPokeV3_22M,
        BattleDecisionStrategy.MiniPokeV2_1_22M_Search, BattleDecisionStrategy.MiniPokeV3_22M_Search
    ];

    private CalcBasedBattleDecisionService _calcService;
    private MiniPokeActionGuard _guard;
    private MiniPokeTurnSearch _search;
    private MiniPokeBattleDecisionService _miniPokeV2122MSearchService;
    private MiniPokeBattleDecisionService _miniPokeV322MService;
    private MiniPokeBattleDecisionService _miniPokeV322MSearchService;
    private IShowdownSimulator _simulator;
    private BattleDecisionManager _manager;

    [SetUp]
    public void SetUp()
    {
        var opponentMovesPredictor = Substitute.For<IOpponentMovesPredictor>();
        var randomService = Substitute.For<IRandomService>();
        var searchAlgorithm = Substitute.For<IBattleSearchAlgorithm>();

        _calcService = new CalcBasedBattleDecisionService(randomService, opponentMovesPredictor, searchAlgorithm);
        _guard = new MiniPokeActionGuard(Substitute.For<IBattleDex>());
        _simulator = Substitute.For<IShowdownSimulator>();
        _simulator.IsAvailable.Returns(true);
        _search = new MiniPokeTurnSearch(_simulator, Substitute.For<IBattleMessageParser>());

        // As BattlesModule registers them: MiniPokeV2_1_22M only with search, MiniPokeV3_22M alone and with search
        _miniPokeV2122MSearchService = Service(BattleDecisionStrategy.MiniPokeV2_1_22M_Search, "MiniPokeV2_1_22M");
        _miniPokeV322MService = Service(BattleDecisionStrategy.MiniPokeV3_22M, "MiniPokeV3_22M");
        _miniPokeV322MSearchService = Service(BattleDecisionStrategy.MiniPokeV3_22M_Search, "MiniPokeV3_22M");
        _manager = new BattleDecisionManager(_calcService,
            [_miniPokeV2122MSearchService, _miniPokeV322MService, _miniPokeV322MSearchService]);
    }

    private MiniPokeBattleDecisionService Service(BattleDecisionStrategy strategy, string model) =>
        new(Substitute.For<IMiniPokeModel>(), Substitute.For<IMiniPokeEncoder>(), _calcService, _guard, _search,
            new MiniPokeModelOptions(strategy, $"Models/{model}", strategy.IsSearch()));

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToTheSearchVariant_WhenInputNamesIt()
    {
        // Act
        var result = _manager.TrySetStrategy("minipokev2.1-22m+search", out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV2_1_22M_Search));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV2_1_22M_Search));
            Assert.That(((MiniPokeBattleDecisionService)_manager.GetCurrentService()).CanSearch, Is.True);
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldRefuseTheSearchVariants_WhenTheSimulatorIsMissing()
    {
        // Arrange
        _simulator.IsAvailable.Returns(false);

        // Act
        var result = _manager.TrySetStrategy("minipokev2.1-22m+search", out _);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(_manager.AvailableStrategies.Where(strategy => strategy.IsSearch()), Is.Empty);
            Assert.That(_manager.AvailableStrategies, Does.Contain(BattleDecisionStrategy.MiniPokeV3_22M));
        }
    }

    [Test]
    public void Test_ActiveStrategy_ShouldDefaultToMiniPokeV322MWithSearch()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV3_22M_Search));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_miniPokeV322MSearchService));
        }
    }

    [Test]
    public void Test_GetCurrentService_ShouldReturnCalcService_WhenActiveMiniPokeModelIsNotRegistered()
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

    [TestCase("minipokev3-22m")]
    [TestCase(" MiniPokeV3-22M ")]
    [TestCase("minipoke")]
    public void Test_TrySetStrategy_ShouldSwitchToMiniPokeV322M_WhenInputNamesItOrAsksForTheLatest(string input)
    {
        // Act
        var result = _manager.TrySetStrategy(input, out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV3_22M));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV3_22M));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_miniPokeV322MService));
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldReturnFalse_WhenTheModelIsOnlyOfferedWithSearch()
    {
        // Act
        var result = _manager.TrySetStrategy("minipokev2.1-22m", out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV3_22M_Search));
            Assert.That(_manager.AvailableStrategies, Does.Not.Contain(BattleDecisionStrategy.MiniPokeV2_1_22M));
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldReturnFalse_WhenMiniPokeVersionIsNotRegistered()
    {
        // Arrange
        var manager = new BattleDecisionManager(_calcService, []);

        // Act
        var result = manager.TrySetStrategy("minipokev3-22m", out _);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(manager.AvailableStrategies, Does.Not.Contain(BattleDecisionStrategy.MiniPokeV3_22M));
            Assert.That(manager.TrySetStrategy("minipoke", out _), Is.False);
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
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV3_22M_Search));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV3_22M_Search));
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
