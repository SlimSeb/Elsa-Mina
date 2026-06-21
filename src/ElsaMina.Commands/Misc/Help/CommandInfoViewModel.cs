using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.Help;

public class CommandInfoViewModel : LocalizableViewModel
{
    public ICommand Command { get; init; }
    public string Trigger { get; init; }
}
