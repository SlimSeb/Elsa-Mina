using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.GuessingGame;

public class GuessingGameResultViewModel : LocalizableViewModel
{
    public IReadOnlyDictionary<GuessingGamePlayer, int> Scores { get; init; }
}