using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Polls.ShowPolls;

public class ShowPollsViewModel : LocalizableViewModel
{
    public string BotName { get; set; }
    public string Trigger { get; set; }
    public string RoomId { get; set; }
    public string RoomName { get; set; }
    public IReadOnlyCollection<SavedPoll> Polls { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public TimeZoneInfo TimeZone { get; set; }
}
