using ElsaMina.Commands.Arcade.Caa;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Cloud.Sheets;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Caa;

[TestFixture]
public class AddCaaPointsCommandTest
{
    private ISheetProvider _sheetProvider;
    private IConfiguration _configuration;
    private ISheet _sheet;
    private IContext _context;
    private AddCaaPointsCommand _command;

    [SetUp]
    public void SetUp()
    {
        _sheetProvider = Substitute.For<ISheetProvider>();
        _configuration = Substitute.For<IConfiguration>();
        _sheet = Substitute.For<ISheet>();
        _context = Substitute.For<IContext>();

        _configuration.CaaSpreadsheetName.Returns("CaaSpreadsheet");
        _configuration.CaaSheetName.Returns("CaaSheet");

        _sheetProvider.GetSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_sheet));

        _command = new AddCaaPointsCommand(_sheetProvider, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeCorrect()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("caa_addpoints_help"));
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
        _context.Target.Returns("someuser, notanumber");

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _sheetProvider.DidNotReceive().GetSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddPointsToExistingUser_WhenUserFound()
    {
        _context.Target.Returns("Existing User, 15");
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Username", "Existing User"]));
        _sheet.GetCellAsync(2, 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string>("10"));

        await _command.RunAsync(_context);

        await _sheet.Received(1).SetCellAsync(2, 1, "25", Arg.Any<CancellationToken>());
        await _sheet.Received(1).FlushAsync(Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("caa_addpoints_success", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateNewRow_WhenUserNotFound()
    {
        _context.Target.Returns("newuser, 25");
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Username", "OtherUser"]));

        await _command.RunAsync(_context);

        await _sheet.Received(1).SetCellAsync(1, Arg.Any<int>(), "newuser", Arg.Any<CancellationToken>());
        await _sheet.Received(1).SetCellAsync(2, Arg.Any<int>(), "25", Arg.Any<CancellationToken>());
        await _sheet.Received(1).FlushAsync(Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("caa_addpoints_new_player", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldTreatMissingPointsAsZero_WhenCellIsEmpty()
    {
        _context.Target.Returns("someuser, 10");
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Username", "someuser"]));
        _sheet.GetCellAsync(2, 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string>(null));

        await _command.RunAsync(_context);

        await _sheet.Received(1).SetCellAsync(2, 1, "10", Arg.Any<CancellationToken>());
    }
}
