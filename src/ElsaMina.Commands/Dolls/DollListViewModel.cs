using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Dolls;

public class DollListViewModel : LocalizableViewModel
{
    public required Dictionary<int, List<Doll>> DollsBySize { get; init; }
}
