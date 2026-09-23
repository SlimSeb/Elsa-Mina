using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Alerts.Twitter;

public class TweetAlertViewModel : LocalizableViewModel
{
    public string Username { get; init; }
    public string TweetId { get; init; }
    public string Text { get; init; }
}
