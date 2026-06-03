using System.Globalization;
using System.Text;
using ElsaMina.Commands.ChatLog;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.FileSharing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.ChatLog;

public class ActivityHeatmapCommandTest
{
    private static readonly string[] LOG_FILE_KEYS = ["chatlogs/testroom/2026-05-04.txt"];

    private IFileSharingService _fileSharingService;
    private IRoomsManager _roomsManager;
    private IContext _context;
    private ActivityHeatmapCommand _command;

    [SetUp]
    public void SetUp()
    {
        _fileSharingService = Substitute.For<IFileSharingService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _context = Substitute.For<IContext>();
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.RoomId.Returns("contextroom");
        _context.GetString(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>()).Returns(string.Empty);
        _command = new ActivityHeatmapCommand(_fileSharingService, _roomsManager);
    }

    [Test]
    public void Test_RequiredRank_And_Contract_ShouldMatch()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Admin));
            Assert.That(_command.HelpMessageKey, Is.EqualTo("activity_heatmap_help"));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenUserIsMissing()
    {
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        _context.Received(1).GetString(_command.HelpMessageKey);
        await _fileSharingService.DidNotReceiveWithAnyArgs().ListFilesAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDefaultToContextRoom_WhenRoomOmitted()
    {
        _context.Target.Returns("alice");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        await _command.RunAsync(_context);

        var now = DateTime.UtcNow;
        await _fileSharingService.Received(1).ListFilesAsync(
            $"chatlogs/contextroom/{now.Year:D4}-{now.Month:D2}-",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseGivenMonth_WhenMonthArgumentProvided()
    {
        _context.Target.Returns("alice, testroom, 2026-02");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        await _command.RunAsync(_context);

        await _fileSharingService.Received(1).ListFilesAsync(
            "chatlogs/testroom/2026-02-",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldAcceptMonthWithoutRoom_AndDefaultToContextRoom()
    {
        _context.Target.Returns("alice, 2026-02");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        await _command.RunAsync(_context);

        await _fileSharingService.Received(1).ListFilesAsync(
            "chatlogs/contextroom/2026-02-",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldAcceptMonthBeforeRoom_RegardlessOfOrder()
    {
        _context.Target.Returns("alice, 2026-02, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        await _command.RunAsync(_context);

        await _fileSharingService.Received(1).ListFilesAsync(
            "chatlogs/testroom/2026-02-",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoLogs_WhenNoFilesExist()
    {
        _context.Target.Returns("alice, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("activity_heatmap_no_logs");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoActivity_WhenUserHasNoMessages()
    {
        _context.Target.Returns("alice, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LOG_FILE_KEYS);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream("[12:00:00 UTC] bob: hello\n"));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("activity_heatmap_no_activity", "alice");
        await _fileSharingService.DidNotReceive().CreateFileAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUploadFailed_WhenUploadReturnsNull()
    {
        _context.Target.Returns("alice, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LOG_FILE_KEYS);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream("[12:00:00 UTC] alice: hello\n"));
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("activity_heatmap_upload_failed");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderAndUploadPng_WhenUserHasActivity()
    {
        // Activity spread across hours so the heatmap has multiple non-empty cells.
        var content =
            "[08:00:00 UTC] alice: morning\n" +
            "[08:30:00 UTC] alice: still morning\n" +
            "[12:00:00 UTC] bob: noise\n" +
            "[20:15:00 UTC] alice: evening\n";
        _context.Target.Returns("alice, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LOG_FILE_KEYS);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream(content));
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/heatmap.png");

        await _command.RunAsync(_context);

        // Real ScottPlot rendering produces a non-empty PNG uploaded with the expected metadata.
        await _fileSharingService.Received(1).CreateFileAsync(
            Arg.Is<byte[]>(bytes => bytes.Length > 0),
            Arg.Is<string>(name => name.StartsWith("heatmaps/heatmap-alice-testroom-")),
            description: "Activity heatmap for alice in testroom",
            mimeType: "image/png",
            cancellationToken: Arg.Any<CancellationToken>());
        _context.Received(1).ReplyHtml(
            Arg.Is<string>(html => html.Contains("https://cdn.example.com/heatmap.png") && html.Contains("<img")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldGetLocalizedChartLabels_WhenRendering()
    {
        _context.Target.Returns("alice, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LOG_FILE_KEYS);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream("[12:00:00 UTC] alice: hello\n"));
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/heatmap.png");

        await _command.RunAsync(_context);

        _context.Received(1).GetString("activity_heatmap_chart_title", "alice", "testroom");
        _context.Received(1).GetString("activity_heatmap_x_label", Arg.Any<object[]>());
        _context.Received(1).GetString("activity_heatmap_colorbar_label");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseRoomTimeZoneForAxisLabel_WhenRoomHasTimeZone()
    {
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("test+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        var room = Substitute.For<IRoom>();
        room.TimeZone.Returns(timeZone);
        _roomsManager.GetRoom("testroom").Returns(room);
        _context.Target.Returns("alice, testroom");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LOG_FILE_KEYS);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream("[12:00:00 UTC] alice: hello\n"));
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/heatmap.png");

        await _command.RunAsync(_context);

        _roomsManager.Received(1).GetRoom("testroom");
        _context.Received(1).GetString("activity_heatmap_x_label", "UTC+02:00");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeUserAndRoom_WhenQuerying()
    {
        _context.Target.Returns("Alice Test, Some Room");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(["chatlogs/someroom/2026-05-04.txt"]);
        _fileSharingService.GetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToStream("[12:00:00 UTC] alicetest: hi\n"));
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/heatmap.png");

        await _command.RunAsync(_context);

        var now = DateTime.UtcNow;
        await _fileSharingService.Received(1).ListFilesAsync(
            $"chatlogs/someroom/{now.Year:D4}-{now.Month:D2}-",
            Arg.Any<CancellationToken>());
        _context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallHandleErrorAsync_WhenExceptionOccurs()
    {
        _context.Target.Returns("alice, testroom");
        var exception = new Exception("s3 failure");
        _fileSharingService.ListFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(exception);

        await _command.RunAsync(_context);

        await _context.Received(1).HandleErrorAsync(exception, Arg.Any<CancellationToken>());
    }

    private static Stream ToStream(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));
}
