using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.ChatLog;

public class DayLineCountViewModel : LocalizableViewModel
{
    public required IReadOnlyList<DayLineCountRow> Rows { get; init; }
}
