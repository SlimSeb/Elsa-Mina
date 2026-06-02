using System.Globalization;
using System.Text;
using ElsaMina.Commands.ChatLog;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.FileSharing;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.ChatLog;

public class TopUsersCommandTest
{
    private static readonly string[] LOG_FILE_KEYS = ["chatlogs/testroom/2026-05-01.txt"];

    private IFileSharingService _fileSharingService;
    private ITemplatesManager _templatesManager;
    private IClockService _clockService;
    private TopUsersCommand _command;

    [SetUp]
    public void SetUp()
    {
        _fileSharingService = Substitute.For<IFileSharingService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _clockService = Substitute.For<IClockService>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<div></div>");
        _command = new TopUsersCommand(_fileSharingService, _templatesManager, _clockService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoLogs_WhenNoS3FilesExist()
    {
        var context = BuildContext(string.Empty, "testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("topusers_no_logs");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenTargetIsEmpty()
    {
        var context = BuildContext(string.Empty, "myroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var date = new DateTime(2026, 5, 22, 0, 0, 0, DateTimeKind.Utc);
        _clockService.CurrentUtcDateTime.Returns(date);

        await _command.RunAsync(context);

        await _fileSharingService.Received(1).ListFilesAsync(
            "chatlogs/myroom/2026-05-",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTargetRoomId_WhenTargetIsProvided()
    {
        var context = BuildContext("otherroom", "myroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());
        context.HasSufficientRankInRoom("otherroom", Arg.Any<Rank>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await _command.RunAsync(context);

        var now = DateTime.UtcNow;
        await _fileSharingService.DidNotReceiveWithAnyArgs().ListFilesAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotListFiles_WhenSenderLacksRankInTargetRoom()
    {
        var context = BuildContext("otherroom", "myroom");
        context.HasSufficientRankInRoom("otherroom", Arg.Any<Rank>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await _command.RunAsync(context);

        await _fileSharingService.DidNotReceiveWithAnyArgs().ListFilesAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WithTop20UsersSortedByCount()
    {
        var context = BuildContext(string.Empty, "testroom");
        var lines = Enumerable.Range(1, 25)
            .Select(i => $"[12:00:00 UTC] user{i:D2}: message")
            .ToList();
        lines.Add("[12:00:00 UTC] user01: extra");
        lines.Add("[12:00:00 UTC] user01: extra");

        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LOG_FILE_KEYS);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream(string.Join("\n", lines)));

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "ChatLog/TopUsers",
            Arg.Is<TopUsersViewModel>(vm =>
                vm.Users.Count == 25 &&
                vm.Users[0].UserId == "user01" &&
                vm.Users[0].Count == 3));
        context.Received(1).ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetMaxCountFromTopUser()
    {
        var content =
            "[12:00:00 UTC] alice: m1\n" +
            "[12:01:00 UTC] alice: m2\n" +
            "[12:02:00 UTC] bob: m1\n";
        var context = BuildContext(string.Empty, "testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LOG_FILE_KEYS);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream(content));

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "ChatLog/TopUsers",
            Arg.Is<TopUsersViewModel>(vm => vm.MaxCount == 2));
    }

    private static IContext BuildContext(string target, string roomId)
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns(target);
        context.RoomId.Returns(roomId);
        context.Culture.Returns(CultureInfo.InvariantCulture);
        context.HasSufficientRankInRoom(Arg.Any<string>(), Arg.Any<Rank>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return context;
    }

    private static Stream ToStream(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));
}
