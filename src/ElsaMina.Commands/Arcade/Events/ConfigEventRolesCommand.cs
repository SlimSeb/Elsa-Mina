using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Arcade.Events;

[NamedCommand("configeventroles", Aliases = ["eventroles"])]
public class ConfigEventRolesCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IEventRoleMappingService _eventRoleMappingService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public ConfigEventRolesCommand(IRoomsManager roomsManager,
        IEventRoleMappingService eventRoleMappingService,
        ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _roomsManager = roomsManager;
        _eventRoleMappingService = eventRoleMappingService;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;

    public override string GetTargetRoomId(IContext context) =>
        string.IsNullOrWhiteSpace(context.Target) ? context.RoomId : context.Target.Trim().ToLower();

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = GetTargetRoomId(context);

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("eventroles_room_not_found");
            return;
        }

        if (context.IsPrivateMessage)
        {
            context.Culture = room.Culture;
        }

        var mappings = await _eventRoleMappingService.GetMappingsForRoomAsync(roomId, cancellationToken);

        var viewModel = new EventRoleMappingViewModel
        {
            Culture = context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            Mappings = mappings
        };

        var template = await _templatesManager.GetTemplateAsync("Arcade/Events/EventRoleMappingDashboard", viewModel);
        context.ReplyHtmlPage($"{roomId}eventroles", template.RemoveNewlines());
    }
}
