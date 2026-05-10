using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Arcade.Sheets;

public class ArcadeHallOfFameViewModel : LocalizableViewModel
{
    public ArcadeHallOfFameEntry[] Entries { get; set; } = [];
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public string BotName { get; set; }
    public string Trigger { get; set; }
}