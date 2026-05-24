using ElsaMina.Commands.Misc.Food;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Food;

[TestFixture]
public class RandSaladCommandTest
{
    private ISpoonacularService _spoonacularService;
    private IContext _context;
    private RandSaladCommand _command;

    [SetUp]
    public void SetUp()
    {
        _spoonacularService = Substitute.For<ISpoonacularService>();
        _context = Substitute.For<IContext>();
        _command = new RandSaladCommand(_spoonacularService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        // Arrange
        // (no additional setup)

        // Act
        var rank = _command.RequiredRank;

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeRandSaladHelp()
    {
        // Arrange
        // (no additional setup)

        // Act
        var key = _command.HelpMessageKey;

        // Assert
        Assert.That(key, Is.EqualTo("randsalad_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallServiceWithSaladTag()
    {
        // Arrange
        _spoonacularService.GetRandomRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("<html/>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _spoonacularService.Received(1).GetRandomRecipeHtmlAsync("salad", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WhenServiceReturnsContent()
    {
        // Arrange
        _spoonacularService.GetRandomRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("<html/>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml("<html/>", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoResults_WhenServiceReturnsNull()
    {
        // Arrange
        _spoonacularService.GetRandomRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("spoonacular_no_results");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenServiceThrows()
    {
        // Arrange
        _spoonacularService.GetRandomRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("API failure"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("spoonacular_error");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }
}
