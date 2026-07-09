using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Scattergories;

public class ScattergoriesResultModel : LocalizableViewModel
{
    public int TotalRounds { get; init; }
    public IReadOnlyList<(string Name, int Points)> Scores { get; init; } = [];
}
