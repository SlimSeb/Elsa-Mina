using System.Text;
using ElsaMina.Cloud;
using ElsaMina.Commands.ChatLog;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using Lusamine.Markovify;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.ChatLog;

public class BaseMarkovCommandTest
{
    [NamedCommand("test-markov")]
    private class TestableMarkovCommand : BaseMarkovCommand
    {
        public TestableMarkovCommand(IFileSharingService fileSharingService, IClockService clockService,
            IConfiguration configuration) : base(fileSharingService, clockService, configuration) { }

        public Task<NewlineText> BuildModelForTestAsync(IContext context, string userFilter) =>
            BuildModelAsync(context, userFilter, CancellationToken.None);

        public override Task RunAsync(IContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private IFileSharingService _fileSharingService;
    private IClockService _clockService;
    private IConfiguration _configuration;
    private IContext _context;
    private TestableMarkovCommand _command;

    [SetUp]
    public void SetUp()
    {
        _fileSharingService = Substitute.For<IFileSharingService>();
        _clockService = Substitute.For<IClockService>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();

        _clockService.CurrentUtcDateTime.Returns(new DateTime(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc));
        _configuration.Trigger.Returns("-");
        _context.RoomId.Returns("testroom");

        _command = new TestableMarkovCommand(_fileSharingService, _clockService, _configuration);
    }

    [Test]
    public async Task Test_BuildModelAsync_ShouldReplyNoLogs_WhenNoFilesExist()
    {
        // Arrange
        _fileSharingService.ListFilesAsync("chatlogs/testroom/2026-05-", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        // Act
        var model = await _command.BuildModelForTestAsync(_context, null);

        // Assert
        Assert.That(model, Is.Null);
        _context.Received(1).ReplyLocalizedMessage("markov_no_logs");
    }

    [Test]
    public async Task Test_BuildModelAsync_ShouldReturnModel_WhenFilteredUserHasEnoughUsableMessages()
    {
        // Arrange
        SetupLogFiles(
            BuildLines("Alice", 12) + BuildLines("Bob", 30),
            null,
            BuildLines("A.lice", 8));

        // Act
        var model = await _command.BuildModelForTestAsync(_context, "alice");

        // Assert
        Assert.That(model, Is.Not.Null);
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_BuildModelAsync_ShouldReplyNotEnoughData_WhenFilteredUserHasTooFewUsableMessages()
    {
        // Arrange
        var unusableLines =
            "[12:00:00 UTC] Alice: -markov\n" +
            "[12:00:01 UTC] Alice:    /me waves\n" +
            "[12:00:02 UTC] Alice: !dt pikachu\n" +
            "[12:00:03 UTC] Alice:    \n" +
            "not a log line\n";
        SetupLogFiles(BuildLines("Alice", 19) + unusableLines + BuildLines("Bob", 30));

        // Act
        var model = await _command.BuildModelForTestAsync(_context, "alice");

        // Assert
        Assert.That(model, Is.Null);
        _context.Received(1).ReplyLocalizedMessage("markov_not_enough_data");
    }

    [Test]
    public async Task Test_BuildModelAsync_ShouldUseMessagesFromAllUsers_WhenUserFilterIsNull()
    {
        // Arrange
        SetupLogFiles(BuildLines("Alice", 10) + BuildLines("Bob", 10));

        // Act
        var model = await _command.BuildModelForTestAsync(_context, null);

        // Assert
        Assert.That(model, Is.Not.Null);
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    private void SetupLogFiles(params string[] fileContents)
    {
        var keys = new List<string>();
        for (var i = 0; i < fileContents.Length; i++)
        {
            var key = $"chatlogs/testroom/2026-05-{i + 1:D2}.txt";
            keys.Add(key);
            var stream = fileContents[i] == null
                ? null
                : new MemoryStream(Encoding.UTF8.GetBytes(fileContents[i]));
            _fileSharingService.GetFileAsync(key, Arg.Any<CancellationToken>()).Returns(stream);
        }

        _fileSharingService.ListFilesAsync("chatlogs/testroom/2026-05-", Arg.Any<CancellationToken>())
            .Returns(keys);
    }

    private static string BuildLines(string username, int count)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            builder.Append($"[12:00:00 UTC] {username}: message number {i} from {username} today\n");
        }

        return builder.ToString();
    }
}
