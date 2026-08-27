using ElsaMina.Battles.Commands;
using ElsaMina.Battles.Strategies;
using ElsaMina.Core.Contexts;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Commands;

[TestFixture]
public class BattleStrategyCommandTest
{
    private IBattleDecisionManager _decisionManager;
    private IContext _context;
    private BattleStrategyCommand _command;

    [SetUp]
    public void SetUp()
    {
        _decisionManager = Substitute.For<IBattleDecisionManager>();
        _context = Substitute.For<IContext>();
        _command = new BattleStrategyCommand(_decisionManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowActiveStrategy_WhenNoArgumentsGiven()
    {
        // Arrange
        _context.Target.Returns(string.Empty);
        _decisionManager.ActiveStrategy.Returns(BattleDecisionStrategy.Llm);
        _decisionManager.AvailableStrategies.Returns(
        [
            BattleDecisionStrategy.Llm,
            BattleDecisionStrategy.DamageCalc,
            BattleDecisionStrategy.TypeMatchup,
            BattleDecisionStrategy.Random
        ]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Is<string>(msg =>
            msg.Contains("Current battle decision strategy: **Llm**") &&
            msg.Contains("llm, damagecalc, typematchup, random")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldSwitchStrategy_WhenValidStrategyProvided()
    {
        // Arrange
        _context.Target.Returns("calc");
        _decisionManager.TrySetStrategy("calc", out Arg.Any<BattleDecisionStrategy>())
            .Returns(x =>
            {
                x[1] = BattleDecisionStrategy.DamageCalc;
                return true;
            });

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Is<string>(msg =>
            msg.Contains("Battle decision strategy switched to: **DamageCalc**")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowError_WhenUnknownStrategyProvided()
    {
        // Arrange
        _context.Target.Returns("unknown");
        _decisionManager.TrySetStrategy("unknown", out Arg.Any<BattleDecisionStrategy>())
            .Returns(false);
        _decisionManager.AvailableStrategies.Returns(
        [
            BattleDecisionStrategy.Llm,
            BattleDecisionStrategy.DamageCalc,
            BattleDecisionStrategy.TypeMatchup,
            BattleDecisionStrategy.Random
        ]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Is<string>(msg =>
            msg.Contains("Unknown strategy \"unknown\"")));
    }
}
