using ElsaMina.Commands.Arcade.Sheets;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Cloud.Sheets;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Sheets;

[TestFixture]
public class ArcadePointsCommandTest
{
    private ISheetProvider _sheetProvider;
    private IConfiguration _configuration;
    private ISheet _sheet;
    private IContext _context;
    private ArcadePointsCommand _command;

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

        _command = new ArcadePointsCommand(_sheetProvider, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeCorrect()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("arcade_sheets_points_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetIsEmpty()
    {
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _sheetProvider.DidNotReceive().GetSheetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoPoints_WhenUserNotFoundInSheet()
    {
        _context.Target.Returns("unknownuser");
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Username", "OtherUser"]));
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Points", "50"]));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("arcade_sheets_points_no_points", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHasPoints_WhenUserFoundInSheet()
    {
        _context.Target.Returns("someuser");
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Username", "someuser"]));
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Points", "42"]));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("arcade_sheets_points_has_points", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeName_WhenLookingUpUser()
    {
        _context.Target.Returns("Some User");
        _sheet.GetColumnAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Username", "Some User"]));
        _sheet.GetColumnAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["Points", "10"]));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("arcade_sheets_points_has_points", Arg.Any<object[]>());
    }
}
