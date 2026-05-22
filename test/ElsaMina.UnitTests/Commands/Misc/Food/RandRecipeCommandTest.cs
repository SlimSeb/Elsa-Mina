using ElsaMina.Commands.Misc.Food;
using ElsaMina.Core.Contexts;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Food;

[TestFixture]
public class RandRecipeCommandTest
{
    private ISpoonacularService _spoonacularService;
    private IContext _context;
    private RandRecipeCommand _command;

    [SetUp]
    public void SetUp()
    {
        _spoonacularService = Substitute.For<ISpoonacularService>();
        _context = Substitute.For<IContext>();
        _command = new RandRecipeCommand(_spoonacularService);
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeRandRecipeHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("randrecipe_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallServiceWithNullTag()
    {
        _spoonacularService.GetRandomRecipeHtmlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("<html/>"));

        await _command.RunAsync(_context);

        await _spoonacularService.Received(1).GetRandomRecipeHtmlAsync(null, Arg.Any<CancellationToken>());
    }
}
