using ElsaMina.Battles;
using ElsaMina.Battles.Strategies.Llm;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Core.Services.LanguageModel;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Battles.Strategies.Llm;

[TestFixture]
public class LlmBattleDecisionServiceTest
{
    private ILanguageModelProvider _languageModelProvider;
    private IOpponentMovesPredictor _opponentMovesPredictor;
    private ILlmBattlePromptBuilder _promptBuilder;
    private ILlmBattleDecisionParser _decisionParser;
    private IBattleDecisionService _fallbackDecisionService;
    private LlmBattleDecisionService _service;

    [SetUp]
    public void SetUp()
    {
        _languageModelProvider = Substitute.For<ILanguageModelProvider>();
        _opponentMovesPredictor = Substitute.For<IOpponentMovesPredictor>();
        _promptBuilder = Substitute.For<ILlmBattlePromptBuilder>();
        _decisionParser = Substitute.For<ILlmBattleDecisionParser>();
        _fallbackDecisionService = Substitute.For<IBattleDecisionService>();

        _promptBuilder.BuildSystemPrompt().Returns("System prompt");
        _promptBuilder.BuildTeamPreviewPrompt(Arg.Any<BattleContext>(), Arg.Any<OpponentPrediction>())
            .Returns("Team preview prompt");
        _promptBuilder.BuildForcedSwitchPrompt(Arg.Any<BattleContext>(), Arg.Any<OpponentPrediction>(), Arg.Any<IReadOnlyList<int>>())
            .Returns("Forced switch prompt");
        _promptBuilder.BuildTurnPrompt(Arg.Any<BattleContext>(), Arg.Any<OpponentPrediction>())
            .Returns("Turn prompt");

        _service = new LlmBattleDecisionService(
            _languageModelProvider,
            _opponentMovesPredictor,
            _promptBuilder,
            _decisionParser,
            _fallbackDecisionService);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenBattleIsOverOrWaiting()
    {
        // Arrange
        var overContext = new BattleContext("battle-gen9ou-1") { IsBattleOver = true };
        var waitContext = new BattleContext("battle-gen9ou-2") { Wait = true };

        // Act
        var overResult = await _service.GetDecisionAsync(overContext);
        var waitResult = await _service.GetDecisionAsync(waitContext);

        // Assert
        Assert.That(overResult, Is.Null);
        Assert.That(waitResult, Is.Null);
        await _languageModelProvider.DidNotReceiveWithAnyArgs().AskLanguageModelAsync(Arg.Any<LanguageModelRequest>());
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnTeamPreviewDecision_WhenTeamPreviewAndLlmSucceeds()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-3")
        {
            TeamPreview = true,
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", CurrentHp = 300, MaxHp = 300 },
                new BattlePokemonState { Ident = "p1: Heatran", CurrentHp = 280, MaxHp = 280 }
            ]
        };

        _languageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("{\"decision\": \"teampreview\", \"index\": 2}");

        _decisionParser.Parse("{\"decision\": \"teampreview\", \"index\": 2}")
            .Returns(LlmDecisionParsedResult.Valid(BattleDecisionType.TeamPreview, 2));

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Not.Null);
        Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.TeamPreview));
        Assert.That(decision.Choices, Is.EqualTo(new List<int> { 2 }));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallback_WhenTeamPreviewLlmReturnsInvalidChoice()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-4")
        {
            TeamPreview = true,
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", CurrentHp = 300, MaxHp = 300 }
            ]
        };

        _languageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("{\"decision\": \"teampreview\", \"index\": 5}"); // Out of bounds

        _decisionParser.Parse(Arg.Any<string>())
            .Returns(LlmDecisionParsedResult.Valid(BattleDecisionType.TeamPreview, 5));

        var fallbackDecision = new BattleDecision(BattleDecisionType.TeamPreview, [1]);
        _fallbackDecisionService.GetDecisionAsync(context, Arg.Any<CancellationToken>())
            .Returns(fallbackDecision);

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.EqualTo(fallbackDecision));
        await _fallbackDecisionService.Received(1).GetDecisionAsync(context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnSwitchDecision_WhenForcedSwitchAndSingleCandidate()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-5")
        {
            ForceSwitchSlots = [true],
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", IsFainted = true, CurrentHp = 0, MaxHp = 300 },
                new BattlePokemonState { Ident = "p1: Heatran", IsActive = false, CurrentHp = 280, MaxHp = 280 }
            ]
        };

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Not.Null);
        Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Switch));
        Assert.That(decision.Choices, Is.EqualTo(new List<int> { 2 }));
        await _languageModelProvider.DidNotReceiveWithAnyArgs().AskLanguageModelAsync(Arg.Any<LanguageModelRequest>());
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnSwitchDecision_WhenForcedSwitchAndLlmSucceeds()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-6")
        {
            ForceSwitchSlots = [true],
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", IsFainted = true, CurrentHp = 0, MaxHp = 300 },
                new BattlePokemonState { Ident = "p1: Heatran", IsActive = false, CurrentHp = 280, MaxHp = 280 },
                new BattlePokemonState { Ident = "p1: Corviknight", IsActive = false, CurrentHp = 290, MaxHp = 290 }
            ]
        };

        _languageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("{\"decision\": \"switch\", \"index\": 3}");

        _decisionParser.Parse("{\"decision\": \"switch\", \"index\": 3}")
            .Returns(LlmDecisionParsedResult.Valid(BattleDecisionType.Switch, 3));

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Not.Null);
        Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Switch));
        Assert.That(decision.Choices, Is.EqualTo(new List<int> { 3 }));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnMoveDecision_WhenTurnAndLlmPicksMove()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-7")
        {
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", IsActive = true, CurrentHp = 300, MaxHp = 300 }
            ],
            ActiveSlots =
            [
                new BattleActiveSlot
                {
                    Moves =
                    [
                        new BattleMoveState { Name = "Earthquake", Pp = 10, MaxPp = 10 },
                        new BattleMoveState { Name = "Stone Edge", Pp = 5, MaxPp = 5 }
                    ]
                }
            ]
        };

        _languageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("{\"decision\": \"move\", \"index\": 1, \"terastallize\": false}");

        _decisionParser.Parse(Arg.Any<string>())
            .Returns(LlmDecisionParsedResult.Valid(BattleDecisionType.Move, 1, terastallize: false));

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Not.Null);
        Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
        Assert.That(decision.Choices, Is.EqualTo(new List<int> { 1 }));
        Assert.That(decision.UseTerastallize, Is.False);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnMoveDecisionWithTera_WhenLlmPicksMoveWithTeraAndTeraIsAvailable()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-8")
        {
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", IsActive = true, CurrentHp = 300, MaxHp = 300 }
            ],
            ActiveSlots =
            [
                new BattleActiveSlot
                {
                    CanTerastallize = "Steel",
                    Moves =
                    [
                        new BattleMoveState { Name = "Earthquake", Pp = 10, MaxPp = 10 }
                    ]
                }
            ]
        };

        _languageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("{\"decision\": \"move\", \"index\": 1, \"terastallize\": true}");

        _decisionParser.Parse(Arg.Any<string>())
            .Returns(LlmDecisionParsedResult.Valid(BattleDecisionType.Move, 1, terastallize: true));

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Not.Null);
        Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
        Assert.That(decision.Choices, Is.EqualTo(new List<int> { 1 }));
        Assert.That(decision.UseTerastallize, Is.True);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnMoveDecisionWithoutTera_WhenLlmPicksMoveWithTeraButTeraNotAvailable()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-9")
        {
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", IsActive = true, CurrentHp = 300, MaxHp = 300 }
            ],
            ActiveSlots =
            [
                new BattleActiveSlot
                {
                    CanTerastallize = "", // Already used or unavailable
                    Moves =
                    [
                        new BattleMoveState { Name = "Earthquake", Pp = 10, MaxPp = 10 }
                    ]
                }
            ]
        };

        _languageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("{\"decision\": \"move\", \"index\": 1, \"terastallize\": true}");

        _decisionParser.Parse(Arg.Any<string>())
            .Returns(LlmDecisionParsedResult.Valid(BattleDecisionType.Move, 1, terastallize: true));

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Not.Null);
        Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
        Assert.That(decision.Choices, Is.EqualTo(new List<int> { 1 }));
        Assert.That(decision.UseTerastallize, Is.False); // Sanitized to false
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnSwitchDecision_WhenTurnAndLlmPicksSwitchAndNotTrapped()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-10")
        {
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", IsActive = true, CurrentHp = 300, MaxHp = 300 },
                new BattlePokemonState { Ident = "p1: Corviknight", IsActive = false, CurrentHp = 280, MaxHp = 280 }
            ],
            ActiveSlots =
            [
                new BattleActiveSlot
                {
                    Trapped = false,
                    Moves =
                    [
                        new BattleMoveState { Name = "Earthquake", Pp = 10, MaxPp = 10 }
                    ]
                }
            ]
        };

        _languageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("{\"decision\": \"switch\", \"index\": 2}");

        _decisionParser.Parse(Arg.Any<string>())
            .Returns(LlmDecisionParsedResult.Valid(BattleDecisionType.Switch, 2));

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.Not.Null);
        Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Switch));
        Assert.That(decision.Choices, Is.EqualTo(new List<int> { 2 }));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallback_WhenLlmPicksSwitchWhileTrapped()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-11")
        {
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", IsActive = true, CurrentHp = 300, MaxHp = 300 },
                new BattlePokemonState { Ident = "p1: Corviknight", IsActive = false, CurrentHp = 280, MaxHp = 280 }
            ],
            ActiveSlots =
            [
                new BattleActiveSlot
                {
                    Trapped = true, // Trapped! Cannot switch
                    Moves =
                    [
                        new BattleMoveState { Name = "Earthquake", Pp = 10, MaxPp = 10 }
                    ]
                }
            ]
        };

        _languageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("{\"decision\": \"switch\", \"index\": 2}");

        _decisionParser.Parse(Arg.Any<string>())
            .Returns(LlmDecisionParsedResult.Valid(BattleDecisionType.Switch, 2));

        var fallbackDecision = new BattleDecision(BattleDecisionType.Move, [1]);
        _fallbackDecisionService.GetDecisionAsync(context, Arg.Any<CancellationToken>())
            .Returns(fallbackDecision);

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.EqualTo(fallbackDecision));
        await _fallbackDecisionService.Received(1).GetDecisionAsync(context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallback_WhenLlmThrowsException()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-12")
        {
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", IsActive = true, CurrentHp = 300, MaxHp = 300 }
            ],
            ActiveSlots =
            [
                new BattleActiveSlot
                {
                    Moves =
                    [
                        new BattleMoveState { Name = "Earthquake", Pp = 10, MaxPp = 10 }
                    ]
                }
            ]
        };

        _languageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network failure"));

        var fallbackDecision = new BattleDecision(BattleDecisionType.Move, [1]);
        _fallbackDecisionService.GetDecisionAsync(context, Arg.Any<CancellationToken>())
            .Returns(fallbackDecision);

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.EqualTo(fallbackDecision));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldDelegateToFallback_WhenDoubles()
    {
        // Arrange
        var context = new BattleContext("battle-gen9vgc-13")
        {
            ActiveSlots =
            [
                new BattleActiveSlot(),
                new BattleActiveSlot()
            ]
        };

        var fallbackDecision = new BattleDecision(BattleDecisionType.Move, [1, 2]);
        _fallbackDecisionService.GetDecisionAsync(context, Arg.Any<CancellationToken>())
            .Returns(fallbackDecision);

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.EqualTo(fallbackDecision));
        await _fallbackDecisionService.Received(1).GetDecisionAsync(context, Arg.Any<CancellationToken>());
        await _languageModelProvider.DidNotReceiveWithAnyArgs().AskLanguageModelAsync(Arg.Any<LanguageModelRequest>());
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldFallbackSafely_WhenPromptBuilderThrowsException()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-14")
        {
            SidePokemon =
            [
                new BattlePokemonState { Ident = "p1: Garchomp", IsActive = true, CurrentHp = 300, MaxHp = 300 }
            ],
            ActiveSlots =
            [
                new BattleActiveSlot
                {
                    Moves = [new BattleMoveState { Name = "Earthquake", Pp = 10, MaxPp = 10 }]
                }
            ]
        };

        _promptBuilder.BuildTurnPrompt(Arg.Any<BattleContext>(), Arg.Any<OpponentPrediction>())
            .Throws(new NullReferenceException("Unexpected state"));

        var fallbackDecision = new BattleDecision(BattleDecisionType.Move, [1]);
        _fallbackDecisionService.GetDecisionAsync(context, Arg.Any<CancellationToken>())
            .Returns(fallbackDecision);

        // Act
        var decision = await _service.GetDecisionAsync(context);

        // Assert
        Assert.That(decision, Is.EqualTo(fallbackDecision));
        await _fallbackDecisionService.Received(1).GetDecisionAsync(context, Arg.Any<CancellationToken>());
    }
}
