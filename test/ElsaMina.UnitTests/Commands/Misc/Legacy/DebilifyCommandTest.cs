using ElsaMina.Commands.Misc.Legacy;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Legacy;

public class DebilifyCommandTest
{
    private IRandomService _randomService;
    private IContext _context;
    private DebilifyCommand _command;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>();
        _context = Substitute.For<IContext>();
        _command = new DebilifyCommand(_randomService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallRandomElementForEachKnownLetter()
    {
        // Arrange
        _context.Target.Returns("abc");
        _randomService.RandomElement(Arg.Any<IEnumerable<string>>()).Returns("x");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _randomService.Received(3).RandomElement(Arg.Any<IEnumerable<string>>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPreserveUnknownCharacters()
    {
        // Arrange
        _context.Target.Returns("a!b");
        _randomService.RandomElement(Arg.Any<IEnumerable<string>>()).Returns("Z");

        // Act
        await _command.RunAsync(_context);

        // Assert
        // '!' is not in SUBSTITUTIONS, so RandomElement is only called for 'a' and 'b'
        _randomService.Received(2).RandomElement(Arg.Any<IEnumerable<string>>());
        _context.Received(1).Reply(Arg.Is<string>(s => s.Contains('!')), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithResultPrefix()
    {
        // Arrange
        _context.Target.Returns("hi");
        _randomService.RandomElement(Arg.Any<IEnumerable<string>>()).Returns("X");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Is<string>(s => s.StartsWith("**Result:**")), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleEmptyTarget()
    {
        // Arrange
        _context.Target.Returns("");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _randomService.DidNotReceive().RandomElement(Arg.Any<IEnumerable<string>>());
        _context.Received(1).Reply(Arg.Is<string>(s => s.Contains("**Result:**")), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCallRandomElementForSpaces()
    {
        // Arrange
        _context.Target.Returns("a b");
        _randomService.RandomElement(Arg.Any<IEnumerable<string>>()).Returns("X");

        // Act
        await _command.RunAsync(_context);

        // Assert
        // Space is not in SUBSTITUTIONS, only 'a' and 'b' trigger RandomElement
        _randomService.Received(2).RandomElement(Arg.Any<IEnumerable<string>>());
        _context.Received(1).Reply(Arg.Is<string>(s => s.Contains(' ')), rankAware: true);
    }
}
