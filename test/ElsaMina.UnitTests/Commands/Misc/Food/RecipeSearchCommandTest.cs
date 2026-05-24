using ElsaMina.Commands.Misc.Food;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Food;

[TestFixture]
public class RecipeSearchCommandTest
{
    private ISpoonacularService _spoonacularService;
    private IContext _context;
    private RecipeSearchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _spoonacularService = Substitute.For<ISpoonacularService>();
        _context = Substitute.For<IContext>();
        _command = new RecipeSearchCommand(_spoonacularService);
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
    public void Test_HelpMessageKey_ShouldBeRecipeHelp()
    {
        // Arrange
        // (no additional setup)

        // Act
        var key = _command.HelpMessageKey;

        // Assert
        Assert.That(key, Is.EqualTo("recipe_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMissingQuery_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("recipe_missing_query");
        await _spoonacularService.DidNotReceive().SearchRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMissingQuery_WhenTargetIsWhitespace()
    {
        // Arrange
        _context.Target.Returns("   ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("recipe_missing_query");
        await _spoonacularService.DidNotReceive().SearchRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallServiceWithQuery_WhenTargetIsProvided()
    {
        // Arrange
        _context.Target.Returns("pasta carbonara");
        _spoonacularService.SearchRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("<html/>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _spoonacularService.Received(1).SearchRecipeHtmlAsync("pasta carbonara", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WhenServiceReturnsContent()
    {
        // Arrange
        _context.Target.Returns("lasagna");
        _spoonacularService.SearchRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
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
        _context.Target.Returns("unknownfood");
        _spoonacularService.SearchRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
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
        _context.Target.Returns("chicken");
        _spoonacularService.SearchRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("API failure"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("spoonacular_error");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }
}
