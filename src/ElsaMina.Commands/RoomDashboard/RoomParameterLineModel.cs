using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.RoomDashboard;

public class RoomParameterLineModel : LocalizableViewModel
{
    public Parameter ParameterKey { get; set; }
    public IParameterDefinition RoomParameterDefinition { get; init; }
    public string CurrentValue { get; init; }
    public string BotName { get; set; }
    public string Trigger { get; set; }
    public string RoomId { get; set; }
    public bool IsBoolean => RoomParameterDefinition?.Type == RoomBotConfigurationType.Boolean;
    public bool BooleanValue => CurrentValue.ToBoolean();
}