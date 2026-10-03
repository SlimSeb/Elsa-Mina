using ElsaMina.Core.Services.Smogon;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Showdown.SmogonStats;

public class SmogonStatsViewModel : LocalizableViewModel
{
    public required string PokemonName { get; init; }
    public required string Format { get; init; }
    public required string Month { get; init; }
    public required Level PlayerLevel { get; init; }
    public required int Rank { get; init; }
    public required double Usage { get; init; }
    public required int RawCount { get; init; }
    public required int TotalBattles { get; init; }
    public required IReadOnlyList<SmogonUsageEntry> TopMoves { get; init; }
    public required IReadOnlyList<SmogonUsageEntry> TopItems { get; init; }
    public required IReadOnlyList<SmogonUsageEntry> TopAbilities { get; init; }
    public required IReadOnlyList<SmogonUsageEntry> TopTeammates { get; init; }
}
