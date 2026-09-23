using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Alerts.Twitch;

public class TwitchLiveAlertViewModel : LocalizableViewModel
{
    public string ChannelLogin { get; init; }
    public string ChannelDisplayName { get; init; }
    public string Title { get; init; }
    public string GameName { get; init; }
    public string ThumbnailUrl { get; init; }
}
