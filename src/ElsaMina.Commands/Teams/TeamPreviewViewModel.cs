using ElsaMina.Core.Services.Teams;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Teams;

public class TeamPreviewViewModel : LocalizableViewModel
{
    public IReadOnlyList<PokemonSet> Team { get; init; }
    public string Sender { get; init; }
    public string Author { get; init; }
}