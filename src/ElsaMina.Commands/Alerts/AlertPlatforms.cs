namespace ElsaMina.Commands.Alerts;

public static class AlertPlatforms
{
    public const string TWITCH = "twitch";
    public const string YOUTUBE = "youtube";
    public const string YOUTUBE_LIVE = "youtubelive";
    public const string TWITTER = "twitter";

    public static readonly IReadOnlyList<string> ALL_PLATFORMS = [TWITCH, YOUTUBE, YOUTUBE_LIVE, TWITTER];

    private static readonly IReadOnlyDictionary<string, string> PLATFORMS_BY_NAME = new Dictionary<string, string>
    {
        [TWITCH] = TWITCH,
        [YOUTUBE] = YOUTUBE,
        ["yt"] = YOUTUBE,
        ["youtubevideo"] = YOUTUBE,
        ["youtubevideos"] = YOUTUBE,
        [YOUTUBE_LIVE] = YOUTUBE_LIVE,
        ["ytlive"] = YOUTUBE_LIVE,
        ["youtubestream"] = YOUTUBE_LIVE,
        [TWITTER] = TWITTER,
        ["x"] = TWITTER,
        ["tweet"] = TWITTER,
        ["tweets"] = TWITTER
    };

    public static bool TryResolve(string input, out string platform)
    {
        platform = null;
        return input != null && PLATFORMS_BY_NAME.TryGetValue(input.ToLowerInvariant(), out platform);
    }
}
