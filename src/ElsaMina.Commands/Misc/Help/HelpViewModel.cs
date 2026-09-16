using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.Help;

public class HelpViewModel : LocalizableViewModel
{
    public string BotName { get; set; }
    public string Trigger { get; set; }
    public string RepositoryLink { get; set; }
    public string Version { get; set; }
}