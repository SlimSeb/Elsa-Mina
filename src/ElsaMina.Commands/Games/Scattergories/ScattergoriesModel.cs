using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Scattergories;

public class ScattergoriesModel : LocalizableViewModel
{
    public int RoundNumber { get; init; }
    public int TotalRounds { get; init; }
    public char Letter { get; init; }
    public int RoundDurationSeconds { get; init; }
    public bool IsRoundOver { get; init; }
    public int EligibleCount { get; init; }
    public IReadOnlyList<ScattergoriesFoundPokemon> Found { get; init; } = [];
    public IReadOnlyList<(string Name, int Points)> Scores { get; init; } = [];
}
