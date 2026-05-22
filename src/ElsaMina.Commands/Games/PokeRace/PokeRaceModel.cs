using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.PokeRace;

public class PokeRaceModel : LocalizableViewModel
{
    public required IReadOnlyDictionary<string, (string Name, string Pokemon)> Players { get; init; }
    public IReadOnlyCollection<string> ChosenPokemons { get; init; } = [];
    public IReadOnlyList<string> Finished { get; init; } = [];
    public IReadOnlyList<(string Pokemon, double Position)> SortedPositions { get; init; } = [];
    public IReadOnlyList<string> AllEvents { get; init; } = [];
    public int Turn { get; init; }
}
