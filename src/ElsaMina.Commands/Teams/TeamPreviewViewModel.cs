using ElsaMina.Core.Services.Templates;
using ElsaMina.Showdown.Teams;

namespace ElsaMina.Commands.Teams;

public class TeamPreviewViewModel : LocalizableViewModel
{
    public IReadOnlyList<PokemonSet> Team { get; init; }
    public string Sender { get; init; }
    public string Author { get; init; }
}