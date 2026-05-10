using ElsaMina.Commands.Arcade.Sheets;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Sheets;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Sheets;

[TestFixture]
public class ArcadeHallOfFameCommandTest
{
    private ISheetProvider _sheetProvider;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private ISheet _sheet;
    private IContext _context;
    private ArcadeHallOfFameCommand _command;

    [SetUp]
    public void SetUp()
    {
        _sheetProvider = Substitute.For<ISheetProvider>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _sheet = Substitute.For<ISheet>();
        _context = Substitute.For<IContext>();

        _configuration.ArcadeSpreadsheetName.Returns("TestSpreadsheet");
        _configuration.ArcadeHallOfFameSheetName.Returns("HallOfFame");

        _sheetProvider.GetSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_sheet));

        _sheet.GetColumnAsync(0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Rank", "1", "2"]));
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Username", "PlayerOne", "PlayerTwo"]));
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Points", "100", "80"]));

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<ArcadeHallOfFameViewModel>())
            .Returns(Task.FromResult("<html/>"));

        _command = new ArcadeHallOfFameCommand(_sheetProvider, _templatesManager, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallTemplate_WithSheetData()
    {
        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Sheets/ArcadeHallOfFame",
            Arg.Is<ArcadeHallOfFameViewModel>(vm => vm.Entries.Length == 2));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtmlPage_WithRenderedTemplate()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("arcade-hof", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSkipInvalidEntries_WhenSheetRowsAreEmpty()
    {
        _sheet.GetColumnAsync(0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Rank", "1", ""]));
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Username", "PlayerOne", ""]));
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Points", "100", ""]));

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Sheets/ArcadeHallOfFame",
            Arg.Is<ArcadeHallOfFameViewModel>(vm => vm.Entries.Length == 1));
    }
}
