using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Development.HandlerDashboard;

public class HandlerDashboardViewModel : LocalizableViewModel
{
    public required string BotName { get; init; }
    public required string Trigger { get; init; }
    public required IReadOnlyList<HandlerLineModel> Handlers { get; init; }
}
