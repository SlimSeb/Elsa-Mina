using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.ChatLog;

public class LinecountViewModel : LocalizableViewModel
{
    public required IReadOnlyList<LinecountDay> Days { get; init; }
    public required int Month { get; init; }
    public required int MaxCount { get; init; }
    public required int TotalCount { get; init; }
    public required double AvgPerDay { get; init; }
}
