using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Llm;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Core.Services.LanguageModel;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Strategies;

[TestFixture]
public class BattleDecisionManagerTest
{
    private LlmBattleDecisionService _llmService;
    private CalcBasedBattleDecisionService _calcService;
    private TypeMatchupBattleDecisionService _typeMatchupService;
    private RandomBattleDecisionService _randomService;
    private MiniPokeBattleDecisionService _miniPokeV11MService;
    private MiniPokeBattleDecisionService _miniPokeV14MService;
    private MiniPokeBattleDecisionService _miniPokeV21MService;
    private BattleDecisionManager _manager;

    [SetUp]
    public void SetUp()
    {
        var languageModelProvider = Substitute.For<ILanguageModelProvider>();
        var opponentMovesPredictor = Substitute.For<IOpponentMovesPredictor>();
        var promptBuilder = Substitute.For<ILlmBattlePromptBuilder>();
        var decisionParser = Substitute.For<ILlmBattleDecisionParser>();
        var randomService = Substitute.For<IRandomService>();
        var searchAlgorithm = Substitute.For<IBattleSearchAlgorithm>();

        _calcService = new CalcBasedBattleDecisionService(randomService, opponentMovesPredictor, searchAlgorithm);
        _llmService = new LlmBattleDecisionService(languageModelProvider, opponentMovesPredictor, promptBuilder, decisionParser, _calcService);
        _typeMatchupService = new TypeMatchupBattleDecisionService(randomService);
        _randomService = new RandomBattleDecisionService(randomService);
        _miniPokeV11MService = new MiniPokeBattleDecisionService(Substitute.For<IMiniPokeModel>(),
            Substitute.For<IMiniPokeEncoder>(), _calcService, randomService,
            new MiniPokeModelOptions(BattleDecisionStrategy.MiniPokeV1_1M, "Models/MiniPokeV1_1M"));
        _miniPokeV14MService = new MiniPokeBattleDecisionService(Substitute.For<IMiniPokeModel>(),
            Substitute.For<IMiniPokeEncoder>(), _calcService, randomService,
            new MiniPokeModelOptions(BattleDecisionStrategy.MiniPokeV1_4M, "Models/MiniPokeV1_4M"));
        _miniPokeV21MService = new MiniPokeBattleDecisionService(Substitute.For<IMiniPokeModel>(),
            Substitute.For<IMiniPokeEncoder>(), _calcService, randomService,
            new MiniPokeModelOptions(BattleDecisionStrategy.MiniPokeV2_1M, "Models/MiniPokeV2_1M"));

        _manager = new BattleDecisionManager(_llmService, _calcService, _typeMatchupService, _randomService,
            [_miniPokeV11MService, _miniPokeV14MService, _miniPokeV21MService]);
    }

    [Test]
    public void Test_ActiveStrategy_ShouldDefaultToDamageCalc()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_calcService));
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToLlm_WhenInputIsLlm()
    {
        // Act
        var result = _manager.TrySetStrategy("llm", out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.Llm));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.Llm));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_llmService));
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToDamageCalc_WhenInputIsCalc()
    {
        // Arrange
        _manager.ActiveStrategy = BattleDecisionStrategy.Llm;

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

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToTypeMatchup_WhenInputIsType()
    {
        // Act
        var result = _manager.TrySetStrategy("type", out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.TypeMatchup));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.TypeMatchup));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_typeMatchupService));
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToRandom_WhenInputIsRandom()
    {
        // Act
        var result = _manager.TrySetStrategy("random", out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.Random));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.Random));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_randomService));
        }
    }

    [TestCase("minipokev1-1m")]
    [TestCase(" MiniPokeV1-1M ")]
    public void Test_TrySetStrategy_ShouldSwitchToMiniPokeV11M_WhenInputNamesIt(string input)
    {
        // Act
        var result = _manager.TrySetStrategy(input, out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV1_1M));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV1_1M));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_miniPokeV11MService));
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToMiniPokeV14M_WhenInputNamesIt()
    {
        // Act
        var result = _manager.TrySetStrategy("minipokev1-4m", out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV1_4M));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV1_4M));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_miniPokeV14MService));
        }
    }

    [TestCase("minipokev2-1m")]
    [TestCase("minipoke")]
    public void Test_TrySetStrategy_ShouldSwitchToMiniPokeV21M_WhenInputNamesItOrAsksForTheLatest(string input)
    {
        // Act
        var result = _manager.TrySetStrategy(input, out var strategy);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV2_1M));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.MiniPokeV2_1M));
            Assert.That(_manager.GetCurrentService(), Is.SameAs(_miniPokeV21MService));
        }
    }

    [Test]
    public void Test_TrySetStrategy_ShouldReturnFalse_WhenMiniPokeVersionIsNotRegistered()
    {
        // Arrange
        var manager = new BattleDecisionManager(_llmService, _calcService, _typeMatchupService, _randomService, []);

        // Act
        var result = manager.TrySetStrategy("minipokev1-1m", out _);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(manager.AvailableStrategies, Does.Not.Contain(BattleDecisionStrategy.MiniPokeV1_1M));
            Assert.That(manager.TrySetStrategy("minipoke", out _), Is.False);
        }
    }

    [Test]
    public void Test_AvailableStrategies_ShouldListEveryStrategy()
    {
        // Assert
        Assert.That(_manager.AvailableStrategies, Is.EquivalentTo(Enum.GetValues<BattleDecisionStrategy>()));
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
            Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
            Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldDelegateToActiveService()
    {
        // Arrange
        _manager.ActiveStrategy = BattleDecisionStrategy.Llm;
        var context = new BattleContext("battle-gen9ou-1") { IsBattleOver = true };

        // Act
        var decision = await _manager.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Null);
    }
}
