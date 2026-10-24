using ElsaMina.Battles.Replays;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Replays;

public class ReplayUploadedHandlerTest
{
    private const string REPLAY_URL = "https://replay.pokemonshowdown.com/gen9randombattle-1";

    // What Showdown's rooms.ts uploadReplay sends: |popup| then the message, which starts with |html|
    private static readonly string[] UPLOADED_POPUP =
    [
        "", "popup", "", "html",
        "<p>Your replay has been uploaded! It's available at:</p><p> " +
        $"<a class=\"no-panel-intercept\" href=\"{REPLAY_URL}\" target=\"_blank\">{REPLAY_URL}</a> " +
        $"<copytext value=\"{REPLAY_URL}\">Copy</copytext>"
    ];

    private static readonly string[] FAILED_POPUP =
        ["", "popup", "Your replay could not be saved: <a href=\"https://example.com\">error</a>"];

    private IReplaySavingService _replaySavingService;
    private ReplayUploadedHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _replaySavingService = Substitute.For<IReplaySavingService>();
        _handler = new ReplayUploadedHandler(_replaySavingService);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDownloadReplay_WhenShowdownUploadedIt()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(UPLOADED_POPUP);

        // Assert
        await _replaySavingService.Received(1).OnReplayUploadedAsync(REPLAY_URL, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnorePopup_WhenItIsNotAnUpload()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(FAILED_POPUP);

        // Assert
        await _replaySavingService.DidNotReceiveWithAnyArgs().OnReplayUploadedAsync(null);
    }
}
