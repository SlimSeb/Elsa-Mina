using ElsaMina.Commands.Games.Catalog;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.RoomDashboard;

public class RoomDashboardViewModel : LocalizableViewModel
{
    public string BotName { get; set; }
    public string Trigger { get; set; }
    public IEnumerable<RoomParameterLineModel> RoomParameterLines { get; set; }
    public IEnumerable<RoomDashboardCategoryViewModel> Categories { get; set; }
    public string RoomName { get; set; }
    public string RoomId { get; set; }
    public string Command { get; set; }
    public bool AreGamesMuted { get; set; }

    // Quick Actions
    public bool HasActiveGame { get; set; }
    public string ActiveGameName { get; set; }

    // Options
    public int ParameterCount { get; set; }

    // Quick Start Game
    public IReadOnlyList<GameInfo> AvailableGames { get; set; }

    // Diagnostics
    public int UserCount { get; set; }
    public string RoomLocale { get; set; }
    public string RoomTimeZone { get; set; }
    public string BotUptime { get; set; }
    public string WorkingSetMemory { get; set; }
    public string FrameworkDescription { get; set; }
    public string RuntimeIdentifier { get; set; }
}