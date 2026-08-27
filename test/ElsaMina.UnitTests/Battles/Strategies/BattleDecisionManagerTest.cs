using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Llm;
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

        _manager = new BattleDecisionManager(_llmService, _calcService, _typeMatchupService, _randomService);
    }

    [Test]
    public void Test_ActiveStrategy_ShouldDefaultToDamageCalc()
    {
        Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
        Assert.That(_manager.GetCurrentService(), Is.SameAs(_calcService));
    }

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToLlm_WhenInputIsLlm()
    {
        // Act
        var result = _manager.TrySetStrategy("llm", out var strategy);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.Llm));
        Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.Llm));
        Assert.That(_manager.GetCurrentService(), Is.SameAs(_llmService));
    }

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToDamageCalc_WhenInputIsCalc()
    {
        // Arrange
        _manager.ActiveStrategy = BattleDecisionStrategy.Llm;

        // Act
        var result = _manager.TrySetStrategy("calc", out var strategy);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
        Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
        Assert.That(_manager.GetCurrentService(), Is.SameAs(_calcService));
    }

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToTypeMatchup_WhenInputIsType()
    {
        // Act
        var result = _manager.TrySetStrategy("type", out var strategy);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.TypeMatchup));
        Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.TypeMatchup));
        Assert.That(_manager.GetCurrentService(), Is.SameAs(_typeMatchupService));
    }

    [Test]
    public void Test_TrySetStrategy_ShouldSwitchToRandom_WhenInputIsRandom()
    {
        // Act
        var result = _manager.TrySetStrategy("random", out var strategy);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.Random));
        Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.Random));
        Assert.That(_manager.GetCurrentService(), Is.SameAs(_randomService));
    }

    [Test]
    public void Test_TrySetStrategy_ShouldReturnFalse_WhenInputIsInvalid()
    {
        // Act
        var result = _manager.TrySetStrategy("nonexistent_strategy", out var strategy);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(strategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
        Assert.That(_manager.ActiveStrategy, Is.EqualTo(BattleDecisionStrategy.DamageCalc));
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
