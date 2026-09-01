using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.RoomDashboard;

public class RoomDashboardCategoryViewModel : LocalizableViewModel
{
    public string CategoryKey { get; set; }
    public string TitleKey { get; set; }
    public IEnumerable<RoomParameterLineModel> Parameters { get; set; }
}
