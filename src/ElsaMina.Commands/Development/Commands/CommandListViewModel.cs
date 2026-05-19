using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Development.Commands;

public class CommandListViewModel : LocalizableViewModel
{
    public IReadOnlyList<ICommand> Commands { get; init; }
    public string BotName { get; init; }
    public string Trigger { get; init; }
    public IReadOnlyList<string> Categories { get; init; }
    public int CurrentCategoryIndex { get; init; }
}
