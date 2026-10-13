using ElsaMina.Cloud;
using ElsaMina.Commands.ChatLog;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.ChatLog;

public class ChatLogServiceTest
{
    // 2026-05-21 10:30:15 UTC
    private const long TIMESTAMP = 1779359415;
    private const long NEXT_DAY_TIMESTAMP = TIMESTAMP + 86400;

    private IFileSharingService _fileSharingService;
    private ChatLogService _service;
    private string _roomId;
    private List<string> _uploadedContents;

    [SetUp]
    public void SetUp()
    {
        _fileSharingService = Substitute.For<IFileSharingService>();
        _uploadedContents = [];
        _fileSharingService.CreateFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                using var reader = new StreamReader(callInfo.Arg<Stream>());
                _uploadedContents.Add(reader.ReadToEnd());
                return Task.FromResult<string>("url");
            });
        // Unique room so test runs never share log files
        _roomId = $"testroom{Guid.NewGuid():N}";
        _service = new ChatLogService(_fileSharingService, TimeSpan.FromMilliseconds(20));
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
        var roomDirectory = Path.Combine("chatlogs", _roomId);
        if (Directory.Exists(roomDirectory))
        {
            Directory.Delete(roomDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Test_OnExitingAsync_ShouldUploadTodaysLog_WhenMessagesWereAppended()
    {
        // Arrange
        _service.Append(_roomId, TIMESTAMP, "Slim", "hello");
        _service.Append(_roomId, TIMESTAMP + 5, "Elsa", "hi there");

        // Act
        await _service.OnExitingAsync(CancellationToken.None);

        // Assert
        await _fileSharingService.Received(1).CreateFileAsync(Arg.Any<Stream>(),
            $"chatlogs/{_roomId}/2026-05-21.txt", null, "text/plain", Arg.Any<CancellationToken>());
        Assert.That(_uploadedContents.Single(), Is.EqualTo(
            $"[10:30:15 UTC] Slim: hello{Environment.NewLine}[10:30:20 UTC] Elsa: hi there{Environment.NewLine}"));
    }

    [Test]
    public async Task Test_OnExitingAsync_ShouldNotUpload_WhenNothingWasAppended()
    {
        // Act
        await _service.OnExitingAsync(CancellationToken.None);

        // Assert
        await _fileSharingService.DidNotReceive().CreateFileAsync(Arg.Any<Stream>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_Append_ShouldStartNewFile_WhenDayChanges()
    {
        // Arrange
        _service.Append(_roomId, TIMESTAMP, "Slim", "yesterday");

        // Act
        _service.Append(_roomId, NEXT_DAY_TIMESTAMP, "Slim", "today");
        await _service.OnExitingAsync(CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(Path.Combine("chatlogs", _roomId, "chatlog20260521.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine("chatlogs", _roomId, "chatlog20260522.txt")), Is.True);
            Assert.That(_uploadedContents.Single(), Does.Contain("today").And.Not.Contain("yesterday"));
        }
        await _fileSharingService.Received(1).CreateFileAsync(Arg.Any<Stream>(),
            $"chatlogs/{_roomId}/2026-05-22.txt", null, "text/plain", Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_OnExitingAsync_ShouldNotThrow_WhenUploadFails()
    {
        // Arrange
        _service.Append(_roomId, TIMESTAMP, "Slim", "hello");
        _fileSharingService.CreateFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("S3 down"));

        // Act & Assert
        Assert.DoesNotThrowAsync(() => _service.OnExitingAsync(CancellationToken.None));
    }

    [Test]
    public async Task Test_OnStartingAsync_ShouldUploadLogsPeriodically()
    {
        // Arrange
        _service.Append(_roomId, TIMESTAMP, "Slim", "hello");
        var uploaded = new TaskCompletionSource();
        _fileSharingService
            .When(service => service.CreateFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => uploaded.TrySetResult());

        // Act
        await _service.OnStartingAsync(CancellationToken.None);
        await uploaded.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        await _fileSharingService.Received().CreateFileAsync(Arg.Any<Stream>(),
            $"chatlogs/{_roomId}/2026-05-21.txt", null, "text/plain", Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_Dispose_ShouldBeIdempotent()
    {
        // Arrange
        _service.Append(_roomId, TIMESTAMP, "Slim", "hello");

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            _service.Dispose();
            _service.Dispose();
        });
    }
}
