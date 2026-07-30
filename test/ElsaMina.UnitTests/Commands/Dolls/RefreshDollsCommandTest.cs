using ElsaMina.Commands.Dolls;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Dolls;

public class RefreshDollsCommandTest
{
    private IContext _context;
    private IDollService _dollService;
    private RefreshDollsCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _dollService = Substitute.For<IDollService>();
        _command = new RefreshDollsCommand(_dollService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithTheDollCount_WhenTheRefreshSucceeds()
    {
        // Arrange
        _dollService.RefreshCatalogueAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, Doll>
        {
            ["pikachu"] = new() { Id = "pikachu", Name = "Pikachu", Size = 16, Image = "https://images/pikachu.png" }
        });

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_refresh_success", 1);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithFailure_WhenTheDriveCannotBeRead()
    {
        // Arrange
        _dollService.RefreshCatalogueAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Could not find the drive or folder 'Poupées'"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_refresh_failure",
            "Could not find the drive or folder 'Poupées'");
    }
}
