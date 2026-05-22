using ElsaMina.Commands.Misc.Food;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Food;

[TestFixture]
public class RandPastaCommandTest
{
    private ISpoonacularService _spoonacularService;
    private IContext _context;
    private RandPastaCommand _command;

    [SetUp]
    public void SetUp()
    {
        _spoonacularService = Substitute.For<ISpoonacularService>();
        _context = Substitute.For<IContext>();
        _command = new RandPastaCommand(_spoonacularService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeRandPastaHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("randpasta_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallServiceWithPastaTag()
    {
        _spoonacularService.GetRandomRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("<html/>"));

        await _command.RunAsync(_context);

        await _spoonacularService.Received(1).GetRandomRecipeHtmlAsync("pasta", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WhenServiceReturnsContent()
    {
        _spoonacularService.GetRandomRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("<html/>"));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtml("<html/>", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoResults_WhenServiceReturnsNull()
    {
        _spoonacularService.GetRandomRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string>(null));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("spoonacular_no_results");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenServiceThrows()
    {
        _spoonacularService.GetRandomRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("API failure"));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("spoonacular_error");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }
}
