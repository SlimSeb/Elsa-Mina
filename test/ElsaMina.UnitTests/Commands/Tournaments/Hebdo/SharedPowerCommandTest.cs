using ElsaMina.Commands.Tournaments.Hebdo;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Tournaments.Hebdo;

public class SharedPowerCommandTest
{
    private IContext _context;
    private SharedPowerCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _command = new SharedPowerCommand();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        // Arrange
        // (no additional setup)

        // Act
        var rank = _command.RequiredRank;

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendTourCreateCommand()
    {
        // Arrange
        // (no additional setup)

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("/tour create randombattlemayhem,elim");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendTourName()
    {
        // Arrange
        // (no additional setup)

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("/tour name [Gen 9] Random Battle Shared Power");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendTourRules()
    {
        // Arrange
        // (no additional setup)

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("/tour rules !scalemonsmod,!camomonsmod,!inversemod");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendWallMessage()
    {
        // Arrange
        // (no additional setup)

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("/wall Tournoi en Shared Power !");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendRfaq()
    {
        // Arrange
        // (no additional setup)

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("!rfaq sharedpower");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendExactlyFiveReplies()
    {
        // Arrange
        // (no additional setup)

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(5).Reply(Arg.Any<string>());
    }
}
