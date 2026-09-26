using System.Globalization;
using System.Text;
using ElsaMina.Commands.ChatLog;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Cloud;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.ChatLog;

public class DayLineCountCommandTest
{
    private IFileSharingService _fileSharingService;
    private ITemplatesManager _templatesManager;
    private IUserColorsService _userColorsService;
    private DayLineCountCommand _command;

    [SetUp]
    public void SetUp()
    {
        _userColorsService = Substitute.For<IUserColorsService>();
        _userColorsService.GetUserColor(Arg.Any<string>()).Returns("#000000");
        _fileSharingService = Substitute.For<IFileSharingService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<div></div>");
        _command = new DayLineCountCommand(_fileSharingService, _templatesManager, _userColorsService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRoomOwner()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_IsWhitelistOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsWhitelistOnly, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenDateIsInvalid()
    {
        var context = BuildContext("not-a-date");
        context.GetString(_command.HelpMessageKey).Returns("help text");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Any<string>());
        await _fileSharingService.DidNotReceiveWithAnyArgs().GetFileAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoLogs_WhenS3FileDoesNotExist()
    {
        var context = BuildContext("2026-05-21");
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Stream)null);

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("daylinecount_no_logs");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoMessages_WhenFileHasNoParseableLines()
    {
        var context = BuildContext("2026-05-21");
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream("this line has no valid format\nanother bad line\n"));

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("daylinecount_no_messages");
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchCorrectS3Key_WhenRoomIsFromTarget()
    {
        var context = BuildContext("2026-05-21, someroom");
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Stream)null);

        await _command.RunAsync(context);

        await _fileSharingService.Received(1).GetFileAsync("chatlogs/someroom/2026-05-21.txt", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchCorrectS3Key_WhenRoomFallsBackToContextRoomId()
    {
        var context = BuildContext("2026-05-21");
        context.RoomId.Returns("myroom");
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Stream)null);

        await _command.RunAsync(context);

        await _fileSharingService.Received(1).GetFileAsync("chatlogs/myroom/2026-05-21.txt", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WhenFileHasValidLines()
    {
        var content =
            "[12:34:56 UTC] alice: hello world\n" +
            "[12:35:00 UTC] bob: hi there friend\n" +
            "[12:36:00 UTC] alice: bye\n";
        var context = BuildContext("2026-05-21");
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream(content));

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "ChatLog/DayLineCount",
            Arg.Is<DayLineCountViewModel>(vm =>
                vm.Rows.Count == 2 &&
                vm.Rows[0].UserId == "alice" &&
                vm.Rows[0].Messages == 2 &&
                vm.Rows[1].UserId == "bob" &&
                vm.Rows[1].Messages == 1));
        context.Received(1).ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSortRowsByMessageCountDescending()
    {
        var content =
            "[12:34:56 UTC] alice: msg1\n" +
            "[12:35:00 UTC] bob: msg1\n" +
            "[12:35:10 UTC] bob: msg2\n" +
            "[12:35:20 UTC] bob: msg3\n";
        var context = BuildContext("2026-05-21");
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream(content));

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "ChatLog/DayLineCount",
            Arg.Is<DayLineCountViewModel>(vm =>
                vm.Rows[0].UserId == "bob" &&
                vm.Rows[1].UserId == "alice"));
    }

    private static IContext BuildContext(string target)
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns(target);
        context.RoomId.Returns("testroom");
        context.Culture.Returns(CultureInfo.InvariantCulture);
        return context;
    }

    private static MemoryStream ToStream(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));
}
