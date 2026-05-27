using ElsaMina.Commands.Tournaments.Handlers;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Tournaments.Betting;

public class BettingAnnouncementViewModel : LocalizableViewModel
{
    public TournamentPlayer[] Players { get; init; }
    public string BotName { get; init; }
    public string Trigger { get; init; }
    public string RoomId { get; init; }
    public int SecondsToClose { get; init; }
    public bool IsBettingOpen { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> BetsByPlayer { get; init; }
}