using System.Globalization;
using ElsaMina.Commands.Ai.Calc;
using ElsaMina.Commands.Ai.LanguageModel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Ai.Calc;

public class CalcWithAiCommandTest
{
    private ILanguageModelProvider _mockLanguageModelProvider;
    private IResourcesService _mockResourcesService;
    private IDamageCalculator _mockDamageCalculator;
    private CalcWithAiCommand _command;

    [SetUp]
    public void SetUp()
    {
        _mockLanguageModelProvider = Substitute.For<ILanguageModelProvider>();
        _mockResourcesService = Substitute.For<IResourcesService>();
        _mockDamageCalculator = Substitute.For<IDamageCalculator>();

        _command = new CalcWithAiCommand(
            _mockLanguageModelProvider,
            _mockResourcesService,
            _mockDamageCalculator);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithUsage_WhenTargetIsEmpty()
    {
        // Arrange
        var context = BuildContext(string.Empty);

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyLocalizedMessage("calc_ai_usage");
        await _mockLanguageModelProvider.DidNotReceive()
            .AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenLanguageModelReturnsNothing()
    {
        // Arrange
        var context = BuildContext("how much does gengar do");
        _mockLanguageModelProvider
            .AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns((string)null);

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyLocalizedMessage("calc_ai_error");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenLanguageModelReportsError()
    {
        // Arrange
        var context = BuildContext("what is the weather");
        _mockLanguageModelProvider
            .AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("""{ "error": "not a calc" }""");

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyLocalizedMessage("calc_ai_error");
        _mockDamageCalculator.DidNotReceive().Calculate(Arg.Any<CalcRequestDto>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithFormattedDescription_WhenCalculationSucceeds()
    {
        // Arrange
        var context = BuildContext("+3 LO gengar sludge bomb vs +1 chansey");
        _mockLanguageModelProvider
            .AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("""Here you go: { "move": "Sludge Bomb", "attacker": { "name": "Gengar" }, "defender": { "name": "Chansey" } }""");
        _mockDamageCalculator.Calculate(Arg.Any<CalcRequestDto>())
            .Returns("+3 252+ SpA Life Orb Gengar Sludge Bomb vs. +1 100 HP / 100 SpD Eviolite Chansey: 204-242 (30.6 - 36.3%) -- 52.9% chance to 3HKO");

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            "<code>// +3 252+ SpA Life Orb Gengar Sludge Bomb vs. +1 100 HP / 100 SpD Eviolite Chansey:<br>//&nbsp;&nbsp;204-242 (30.6 - 36.3%) -- 52.9% chance to 3HKO</code>");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenCalculatorThrows()
    {
        // Arrange
        var context = BuildContext("gengar sludge bomb vs chansey");
        _mockLanguageModelProvider
            .AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("""{ "move": "Sludge Bomb", "attacker": { "name": "Gengar" }, "defender": { "name": "Chansey" } }""");
        _mockDamageCalculator.Calculate(Arg.Any<CalcRequestDto>())
            .Returns(_ => throw new InvalidOperationException("Unknown species"));

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyLocalizedMessage("calc_ai_error");
    }

    private static IContext BuildContext(string target)
    {
        var context = Substitute.For<IContext>();
        context.Command.Returns("calc-ai");
        context.Target.Returns(target);
        context.Culture.Returns(CultureInfo.InvariantCulture);
        return context;
    }
}
