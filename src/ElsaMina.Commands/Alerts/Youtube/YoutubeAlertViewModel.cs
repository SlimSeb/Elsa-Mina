using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeAlertViewModel : LocalizableViewModel
{
    public bool IsLive { get; init; }
    public string VideoId { get; init; }
    public string Title { get; init; }
    public string ChannelTitle { get; init; }
    public string ThumbnailUrl { get; init; }
}
