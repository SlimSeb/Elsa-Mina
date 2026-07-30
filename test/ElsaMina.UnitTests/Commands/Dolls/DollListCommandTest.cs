using System.Globalization;
using ElsaMina.Commands.Dolls;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Dolls;

public class DollListCommandTest
{
    private IContext _context;
    private IDollService _dollService;
    private ITemplatesManager _templatesManager;
    private DollListCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _dollService = Substitute.For<IDollService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _context.Culture.Returns(new CultureInfo("en-US"));
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("rendered");
        _command = new DollListCommand(_dollService, _templatesManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithEmptyMessage_WhenCatalogueIsEmpty()
    {
        // Arrange
        _dollService.GetCatalogueAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, Doll>());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_list_empty");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldGroupDollsBySize_WhenCatalogueIsNotEmpty()
    {
        // Arrange
        _dollService.GetCatalogueAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, Doll>
        {
            ["pikachu"] = NewDoll("pikachu", "Pikachu", 16),
            ["clefairy"] = NewDoll("clefairy", "Clefairy", 16),
            ["snorlax"] = NewDoll("snorlax", "Snorlax", 32)
        });

        DollListViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync("Dolls/DollList", Arg.Do<object>(
            viewModel => capturedViewModel = viewModel as DollListViewModel));

        // Act
        await _command.RunAsync(_context);

        // Assert
        Assert.That(capturedViewModel, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(capturedViewModel.DollsBySize.Keys, Is.EqualTo(new[] { 16, 32 }));
            Assert.That(capturedViewModel.DollsBySize[16].Select(doll => doll.Id),
                Is.EqualTo(new[] { "clefairy", "pikachu" }));
            Assert.That(capturedViewModel.DollsBySize[32], Has.Count.EqualTo(1));
        });
        _context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithUnavailableMessage_WhenTheDriveCannotBeRead()
    {
        // Arrange
        _dollService.GetCatalogueAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("drive down"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_catalogue_unavailable", "drive down");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    private static Doll NewDoll(string id, string name, int size)
    {
        return new Doll { Id = id, Name = name, Size = size, Image = $"https://images/{id}.png" };
    }
}
