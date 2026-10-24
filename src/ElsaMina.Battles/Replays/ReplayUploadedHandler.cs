using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Handlers;

namespace ElsaMina.Battles.Replays;

/// <summary>
/// Showdown answers the bot's /savereplay with a popup giving the replay's address (rooms.ts uploadReplay).
/// </summary>
public partial class ReplayUploadedHandler : Handler
{
    private const string UPLOADED_MESSAGE = "Your replay has been uploaded";
    private static readonly Regex REPLAY_URL_REGEX = ReplayUrlRegex();

    private readonly IReplaySavingService _replaySavingService;

    public ReplayUploadedHandler(IReplaySavingService replaySavingService)
    {
        _replaySavingService = replaySavingService;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "popup" };

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 3)
        {
            return Task.CompletedTask;
        }

        var popup = string.Join("|", parts[2..]);
        var match = popup.Contains(UPLOADED_MESSAGE) ? REPLAY_URL_REGEX.Match(popup) : Match.Empty;
        if (match.Success)
        {
            // Downloading takes a while (retries included): in the background, not on the popup's lane
            _ = _replaySavingService.OnReplayUploadedAsync(match.Groups["url"].Value, CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    [GeneratedRegex("href=\"(?<url>https?://[^\"]+)\"", RegexOptions.None, Constants.REGEX_MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex ReplayUrlRegex();
}
