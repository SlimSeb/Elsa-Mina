using ElsaMina.Commands.Arcade.Sheets;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Cloud.Sheets;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Sheets;

[TestFixture]
public class ArcadeSheetAddPointsCommandTest
{
    private ISheetProvider _sheetProvider;
    private IConfiguration _configuration;
    private ISheet _sheet;
    private IContext _context;
    private ArcadeSheetAddPointsCommand _command;

    [SetUp]
    public void SetUp()
    {
        _sheetProvider = Substitute.For<ISheetProvider>();
        _configuration = Substitute.For<IConfiguration>();
        _sheet = Substitute.For<ISheet>();
        _context = Substitute.For<IContext>();

        _configuration.ArcadeSpreadsheetName.Returns("TestSpreadsheet");
        _configuration.ArcadeHallOfFameSheetName.Returns("HallOfFame");

        _sheetProvider.GetSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_sheet));

        _command = new ArcadeSheetAddPointsCommand(_sheetProvider, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeCorrect()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("arcade_sheets_addpoints_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenNoCommaInTarget()
    {
        _context.Target.Returns("justausername");

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _sheetProvider.DidNotReceive().GetSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenPointsIsNotAnInteger()
    {
        _context.Target.Returns("someuser, abc");

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _sheetProvider.DidNotReceive().GetSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUpdateExistingRow_WhenUserIsFound()
    {
        _context.Target.Returns("someuser, 10");
        _sheet.GetColumnAsync(8, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["UserId", "someuser"]));
        _sheet.GetCellAsync(9, 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string>("5"));

        await _command.RunAsync(_context);

        await _sheet.Received(1).SetCellAsync(9, 1, "15", Arg.Any<CancellationToken>());
        await _sheet.Received(1).FlushAsync(Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("arcade_sheets_addpoints_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddNewRow_WhenUserIsNotFound()
    {
        _context.Target.Returns("newuser, 20");
        _sheet.GetColumnAsync(8, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["UserId", "existinguser"]));
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Username", "Existing User"]));

        await _command.RunAsync(_context);

        await _sheet.Received(1).SetCellAsync(8, Arg.Any<int>(), "newuser", Arg.Any<CancellationToken>());
        await _sheet.Received(1).SetCellAsync(9, Arg.Any<int>(), "20", Arg.Any<CancellationToken>());
        await _sheet.Received(1).FlushAsync(Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("arcade_sheets_addpoints_new_player");
    }
}
