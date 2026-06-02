using System.Globalization;
using System.Text;
using ElsaMina.Commands.ChatLog;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.FileSharing;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.ChatLog;

public class LinecountCommandTest
{
    private static readonly string[] LOG_FILE_KEYS = ["chatlogs/testroom/2026-05-01.txt"];

    private IFileSharingService _fileSharingService;
    private ITemplatesManager _templatesManager;
    private LinecountCommand _command;

    [SetUp]
    public void SetUp()
    {
        _fileSharingService = Substitute.For<IFileSharingService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<div></div>");
        _command = new LinecountCommand(_fileSharingService, _templatesManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenMissingRoomArgument()
    {
        var context = BuildContext("alice");
        context.GetString(_command.HelpMessageKey).Returns("help text");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Any<string>());
        await _fileSharingService.DidNotReceiveWithAnyArgs().ListFilesAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoLogs_WhenNoS3FilesExist()
    {
        var context = BuildContext("alice, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("linecount_no_logs");
    }

    [Test]
    public async Task Test_RunAsync_ShouldListFilesWithCorrectMonthPrefix()
    {
        var context = BuildContext("alice, someroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        await _command.RunAsync(context);

        var now = DateTime.UtcNow;
        await _fileSharingService.Received(1).ListFilesAsync(
            $"chatlogs/someroom/{now.Year:D4}-{now.Month:D2}-",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WithDaysOrderedAscending()
    {
        var context = BuildContext("alice, testroom");
        var keys = new[]
        {
            "chatlogs/testroom/2026-05-03.txt",
            "chatlogs/testroom/2026-05-01.txt",
            "chatlogs/testroom/2026-05-02.txt"
        };
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(keys);
        _fileSharingService.GetFileAsync("chatlogs/testroom/2026-05-01.txt", Arg.Any<CancellationToken>())
            .Returns(ToStream("[12:00:00 UTC] alice: hello\n"));
        _fileSharingService.GetFileAsync("chatlogs/testroom/2026-05-02.txt", Arg.Any<CancellationToken>())
            .Returns(ToStream("[12:00:00 UTC] alice: world\n[12:01:00 UTC] alice: again\n"));
        _fileSharingService.GetFileAsync("chatlogs/testroom/2026-05-03.txt", Arg.Any<CancellationToken>())
            .Returns(ToStream("[12:00:00 UTC] bob: hi\n"));

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "ChatLog/Linecount",
            Arg.Is<LinecountViewModel>(vm =>
                vm.Days.Count == 3 &&
                vm.Days[0].Day == 1 && vm.Days[0].Count == 1 &&
                vm.Days[1].Day == 2 && vm.Days[1].Count == 2 &&
                vm.Days[2].Day == 3 && vm.Days[2].Count == 0 &&
                vm.TotalCount == 3 &&
                vm.MaxCount == 2));
        context.Received(1).ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSkipFile_WhenS3ReturnsNullStream()
    {
        var context = BuildContext("alice, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LOG_FILE_KEYS);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Stream)null);

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "ChatLog/Linecount",
            Arg.Is<LinecountViewModel>(vm => vm.TotalCount == 0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldOnlyCountTargetUser_WhenFileContainsManyUsers()
    {
        var content =
            "[12:00:00 UTC] alice: msg1\n" +
            "[12:01:00 UTC] bob: msg1\n" +
            "[12:02:00 UTC] alice: msg2\n";
        var context = BuildContext("alice, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LOG_FILE_KEYS);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream(content));

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "ChatLog/Linecount",
            Arg.Is<LinecountViewModel>(vm => vm.TotalCount == 2));
    }

    private static IContext BuildContext(string target)
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns(target);
        context.Culture.Returns(CultureInfo.InvariantCulture);
        return context;
    }

    private static Stream ToStream(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));
}
