using ElsaMina.Commands.Games.Catalog;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Catalog;

[TestFixture]
public class GamesCommandTest
{
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private GamesCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult("<html>games</html>"));

        _configuration = Substitute.For<IConfiguration>();
        _configuration.Trigger.Returns("-");

        _context = Substitute.For<IContext>();

        _sut = new GamesCommand(_templatesManager, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_sut.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_sut.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplateWithCatalog()
    {
        await _sut.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync("Games/Catalog/Games",
            Arg.Is<GamesViewModel>(model =>
                model.Trigger == "-" && ReferenceEquals(model.Games, GamesCatalog.Games)));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtmlRankAware()
    {
        await _sut.RunAsync(_context);

        _context.Received(1).ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), true);
    }

    [Test]
    public void Test_Catalog_ShouldNotBeEmpty()
    {
        Assert.That(GamesCatalog.Games, Is.Not.Empty);
    }
}
