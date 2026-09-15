using ElsaMina.Commands.Misc.Legacy;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Legacy;

public class EvroMakerCommandTest
{
    private IRandomService _randomService;
    private IContext _context;
    private EvroMakerCommand _command;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>();
        _context = Substitute.For<IContext>();
        _randomService.NextInt(Arg.Any<int>()).Returns(0);
        _randomService.NextDouble().Returns(0.0);
        _command = new EvroMakerCommand(_randomService);
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("single")]
    [TestCase("  single   ")]
    public async Task Test_RunAsync_ShouldNotReply_WhenTargetHasLessThanTwoWords(string target)
    {
        // Arrange
        _context.Target.Returns(target);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.DidNotReceive().Reply(Arg.Any<string>(), Arg.Any<bool>());
        _randomService.DidNotReceive().NextDouble();
    }

    [Test]
    public async Task Test_RunAsync_ShouldAppendStartAndComplement_WhenTwoWordsAndNoRandomTrigger()
    {
        // Arrange
        _context.Target.Returns("hello   world");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("Btw hello  world (je rigole ofc)", rankAware: true);
        _randomService.Received(1).NextDouble();
    }

    [Test]
    public async Task Test_RunAsync_ShouldQuoteWords_WhenQuoteRollsSucceed()
    {
        // Arrange
        _context.Target.Returns("one two three");
        _randomService.NextDouble().Returns(0.9, 0.6, 0.9, 0.9, 0.1, 0.1);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("Btw one \"two\" kek :three:", rankAware: true);
        _randomService.Received(6).NextDouble();
    }

    [Test]
    public async Task Test_RunAsync_ShouldAppendEndingAndAltStrings_WhenRollsSucceed()
    {
        // Arrange
        _context.Target.Returns("a b c d e f g");
        _randomService.NextInt(8).Returns(1);
        _randomService.NextInt(19).Returns(2);
        _randomService.NextInt(5).Returns(3);
        _randomService.NextInt(15).Returns(4);
        _randomService.NextDouble().Returns(0.0, 0.0, 0.0, 0.0, 0.0, 0.8, 0.0, 0.7, 0.0, 0.0, 0.0);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("Euh pk a b c d tbh ué e (ba après g 13 ans) , f  g bref", rankAware: true);
        _randomService.Received(11).NextDouble();
    }

    [Test]
    public async Task Test_RunAsync_ShouldFallThroughToEnding_WhenAltRollFails()
    {
        // Arrange
        _context.Target.Returns("a b c d e f");
        _randomService.NextDouble().Returns(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.5, 0.9, 0.0);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("Btw a b c d e Jsp  f (je rigole ofc)", rankAware: true);
        _randomService.Received(10).NextDouble();
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotRollAlt_WhenWordIsLastEvenIfAltCountIsHigh()
    {
        // Arrange
        _context.Target.Returns("a b c d e");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("Btw a b c d  e (je rigole ofc)", rankAware: true);
        _randomService.Received(7).NextDouble();
    }
}
