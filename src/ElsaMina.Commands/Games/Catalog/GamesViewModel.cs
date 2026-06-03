using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Catalog;

public class GamesViewModel : LocalizableViewModel
{
    public required string Trigger { get; init; }
    public required IReadOnlyList<GameInfo> Games { get; init; }
}
