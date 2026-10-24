using System.Net;
using ElsaMina.Battles;
using ElsaMina.Battles.Replays;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.System;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Battles.Replays;

public class ReplaySavingServiceTest
{
    private const string ROOM_ID = "battle-gen9randombattle-1";
    private const string REPLAY_ID = "gen9randombattle-1";
    private const string REPLAY_URL = "https://replay.pokemonshowdown.com/gen9randombattle-1";
    private const string REPLAY_JSON = "{\"id\":\"gen9randombattle-1\",\"log\":\"|win|Elsa-Mina\"}";
    private static readonly DateTimeOffset NOW = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] LINES = ["|init|battle", "|request|{\"rqid\":1}", "|win|Elsa-Mina"];

    private IBot _bot;
    private IHttpService _httpService;
    private IClockService _clockService;
    private ISystemService _systemService;
    private string _directory;
    private DateTimeOffset _now;
    private ReplaySavingService _service;

    [SetUp]
    public void SetUp()
    {
        _bot = Substitute.For<IBot>();
        _httpService = Substitute.For<IHttpService>();
        _clockService = Substitute.For<IClockService>();
        _systemService = Substitute.For<ISystemService>();
        _now = NOW;
        _clockService.CurrentUtcDateTimeOffset.Returns(_ => _now);
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _directory = Path.Join(Path.GetTempPath(), $"replays_{Guid.NewGuid():N}");
        var configuration = Substitute.For<IBattlesConfiguration>();
        configuration.ReplaysDirectory.Returns(_directory);
        _service = new ReplaySavingService(_bot, _httpService, _clockService, _systemService, configuration);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static BattleContext Battle(string roomId = ROOM_ID)
    {
        var context = new BattleContext(roomId);
        context.Lines.AddRange(LINES);
        return context;
    }

    private void SetUpReplay(string url, string json = REPLAY_JSON)
    {
        _httpService.SendForStringAsync(Arg.Is<HttpRequest>(request => request.Uri == $"{url}.json"),
                Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<string> { Data = json, StatusCode = HttpStatusCode.OK });
    }

    [Test]
    public async Task Test_OnBattleEndedAsync_ShouldWriteLinesAndAskForReplay_WhenSaving()
    {
        // Arrange
        _service.Start();

        // Act
        await _service.OnBattleEndedAsync(Battle());

        // Assert
        var log = await File.ReadAllTextAsync(Path.Join(_directory, $"{REPLAY_ID}.log"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(log, Is.EqualTo(string.Join('\n', LINES) + '\n'));
            Assert.That(_service.Status.WaitingCount, Is.EqualTo(1));
        }

        _bot.Received(1).Say(ROOM_ID, "/savereplay");
    }

    [Test]
    public async Task Test_OnBattleEndedAsync_ShouldDoNothing_WhenNotSaving()
    {
        // Arrange
        _service.Start();
        _service.Stop();

        // Act
        await _service.OnBattleEndedAsync(Battle());

        // Assert
        Assert.That(Directory.Exists(_directory), Is.False);
        _bot.DidNotReceiveWithAnyArgs().Say(null, null);
    }

    [Test]
    public async Task Test_OnReplayUploadedAsync_ShouldDownloadReplay_WhenBotAskedForIt()
    {
        // Arrange
        _service.Start();
        await _service.OnBattleEndedAsync(Battle());
        SetUpReplay(REPLAY_URL);

        // Act
        await _service.OnReplayUploadedAsync(REPLAY_URL);

        // Assert
        var json = await File.ReadAllTextAsync(Path.Join(_directory, $"{REPLAY_ID}.json"));
        var status = _service.Status;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(json, Is.EqualTo(REPLAY_JSON));
            Assert.That(status.SavedCount, Is.EqualTo(1));
            Assert.That(status.WaitingCount, Is.Zero);
            Assert.That(status.LastSaved, Is.EqualTo(REPLAY_ID));
        }
    }

    [TestCase("battle-gen9randombattle-1-abcdefpw", "https://replay.pokemonshowdown.com/gen9randombattle-1-ghijklpw")]
    [TestCase(ROOM_ID, "https://replay.example.com/myserver-gen9randombattle-1")]
    public async Task Test_OnReplayUploadedAsync_ShouldNameFilesAfterBattle_WhenReplayIdHasPasswordOrServer(
        string roomId, string url)
    {
        // Arrange
        _service.Start();
        await _service.OnBattleEndedAsync(Battle(roomId));
        SetUpReplay(url);

        // Act
        await _service.OnReplayUploadedAsync(url);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(Path.Join(_directory, $"{REPLAY_ID}.log")), Is.True);
            Assert.That(File.Exists(Path.Join(_directory, $"{REPLAY_ID}.json")), Is.True);
        }
    }

    [Test]
    public async Task Test_OnReplayUploadedAsync_ShouldIgnoreReplay_WhenBotDidNotAskForIt()
    {
        // Arrange: battle 12's id ends like battle 1's
        _service.Start();
        await _service.OnBattleEndedAsync(Battle());

        // Act
        await _service.OnReplayUploadedAsync("https://replay.pokemonshowdown.com/gen9randombattle-12");

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs().SendForStringAsync(null);
        Assert.That(_service.Status.WaitingCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_OnReplayUploadedAsync_ShouldRetryThenCountFailure_WhenDownloadKeepsFailing()
    {
        // Arrange
        _service.Start();
        await _service.OnBattleEndedAsync(Battle());
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Throws(new HttpException(HttpStatusCode.NotFound, "Not found"));

        // Act
        await _service.OnReplayUploadedAsync(REPLAY_URL);

        // Assert
        await _httpService.Received(ReplaySavingService.MAX_DOWNLOAD_ATTEMPTS)
            .SendForStringAsync(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>());
        await _systemService.Received(ReplaySavingService.MAX_DOWNLOAD_ATTEMPTS - 1)
            .SleepAsync(ReplaySavingService.DOWNLOAD_RETRY_DELAY, Arg.Any<CancellationToken>());
        var status = _service.Status;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.FailedCount, Is.EqualTo(1));
            Assert.That(status.SavedCount, Is.Zero);
            Assert.That(status.WaitingCount, Is.Zero);
        }
    }

    [Test]
    public async Task Test_Status_ShouldCountFailure_WhenShowdownNeverUploadsReplay()
    {
        // Arrange
        _service.Start();
        await _service.OnBattleEndedAsync(Battle());

        // Act
        _now += ReplaySavingService.UPLOAD_TIMEOUT;
        var status = _service.Status;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.FailedCount, Is.EqualTo(1));
            Assert.That(status.WaitingCount, Is.Zero);
        }
    }

    [Test]
    public void Test_Status_ShouldUseConfiguredDirectory()
    {
        // Act
        var status = _service.Status;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.IsSaving, Is.False);
            Assert.That(status.Directory, Is.EqualTo(Path.GetFullPath(_directory)));
        }
    }

    [TestCase(ROOM_ID, REPLAY_ID)]
    [TestCase("battle-gen9randombattle-1-abcdefpw", REPLAY_ID)]
    [TestCase("battle-gen9ou-2345", "gen9ou-2345")]
    public void Test_GetReplayId_ShouldDropPrefixAndPassword(string roomId, string expected)
    {
        // Act
        var replayId = ReplaySavingService.GetReplayId(roomId);

        // Assert
        Assert.That(replayId, Is.EqualTo(expected));
    }
}
