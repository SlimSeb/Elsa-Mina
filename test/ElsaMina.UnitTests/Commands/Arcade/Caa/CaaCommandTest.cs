using ElsaMina.Commands.Arcade.Caa;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Sheets;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Caa;

public class CaaCommandTest
{
    private ISheetProvider _sheetProvider;
    private ISheet _sheet;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private CaaCommand _command;

    [SetUp]
    public void SetUp()
    {
        _sheet = Substitute.For<ISheet>();
        _sheetProvider = Substitute.For<ISheetProvider>();
        _sheetProvider.GetSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_sheet);

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<html>caa</html>");

        _configuration = Substitute.For<IConfiguration>();
        _configuration.CaaSpreadsheetName.Returns("CAA Spreadsheet");
        _configuration.CaaSheetName.Returns("Feuille tampon");

        _context = Substitute.For<IContext>();

        _command = new CaaCommand(_sheetProvider, _templatesManager, _configuration);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.Name, Is.EqualTo("caa"));
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchSheetWithConfiguredNames_WhenCalled()
    {
        _sheet.GetColumnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(["header"]);

        await _command.RunAsync(_context);

        await _sheetProvider.Received(1).GetSheetAsync("CAA Spreadsheet", "Feuille tampon", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSkipHeaderRow_WhenBuildingEntries()
    {
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(["Pseudo", "player1", "player2"]);
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(["Points", "10", "5"]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Caa/CaaTable",
            Arg.Is<CaaViewModel>(vm => vm.Entries.Length == 2)
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldSortEntriesByPointsDescending_WhenBuildingLeaderboard()
    {
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(["Pseudo", "player1", "player2", "player3"]);
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(["Points", "5", "20", "10"]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Caa/CaaTable",
            Arg.Is<CaaViewModel>(vm =>
                vm.Entries[0].UserName == "player2" &&
                vm.Entries[1].UserName == "player3" &&
                vm.Entries[2].UserName == "player1"
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldAssignCorrectRanks_WhenBuildingLeaderboard()
    {
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(["Pseudo", "player1", "player2", "player3"]);
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(["Points", "5", "20", "10"]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Caa/CaaTable",
            Arg.Is<CaaViewModel>(vm =>
                vm.Entries[0].Rank == 1 &&
                vm.Entries[1].Rank == 2 &&
                vm.Entries[2].Rank == 3
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldFilterOutEntriesWithEmptyUsername_WhenBuildingLeaderboard()
    {
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(["Pseudo", "player1", "", "player3"]);
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(["Points", "10", "5", "8"]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Caa/CaaTable",
            Arg.Is<CaaViewModel>(vm => vm.Entries.Length == 2)
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldFilterOutEntriesWithNonNumericPoints_WhenBuildingLeaderboard()
    {
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(["Pseudo", "player1", "player2"]);
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(["Points", "notanumber", "15"]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Caa/CaaTable",
            Arg.Is<CaaViewModel>(vm =>
                vm.Entries.Length == 1 &&
                vm.Entries[0].UserName == "player2"
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldReturnEmptyLeaderboard_WhenSheetOnlyHasHeader()
    {
        _sheet.GetColumnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(["header"]);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Caa/CaaTable",
            Arg.Is<CaaViewModel>(vm => vm.Entries.Length == 0)
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCultureToViewModel_WhenRendering()
    {
        _sheet.GetColumnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(["header"]);
        _context.Culture.Returns(new System.Globalization.CultureInfo("fr-FR"));

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Caa/CaaTable",
            Arg.Is<CaaViewModel>(vm => vm.Culture.Name == "fr-FR")
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallReplyHtml_WhenTemplateRendered()
    {
        _sheet.GetColumnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(["header"]);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<b>leaderboard</b>");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtml("<b>leaderboard</b>", rankAware: Arg.Any<bool>());
    }
}
