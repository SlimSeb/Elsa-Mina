using ElsaMina.Commands.Repeats;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Repeats.List;

public class RepeatsListViewModel : LocalizableViewModel
{
    public IEnumerable<IRepeat> Repeats { get; init; }
    public string BotName { get; init; }
    public string Trigger { get; init; }
}