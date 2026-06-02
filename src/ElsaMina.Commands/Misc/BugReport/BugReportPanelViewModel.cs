using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.BugReport;

public class BugReportPanelViewModel : LocalizableViewModel
{
    public string BotName { get; set; }
    public string Trigger { get; set; }
}
