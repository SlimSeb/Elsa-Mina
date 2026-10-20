using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Arcade.Events;

[NamedCommand("deleteeventrole")]
public class DeleteEventRoleCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IEventRoleMappingService _eventRoleMappingService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public DeleteEventRoleCommand(IRoomsManager roomsManager,
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
    public override bool IsPrivateMessageOnly => true;

    // Format : eventName;;roomId
    public override string GetTargetRoomId(IContext context)
    {
        var parts = context.Target.Split(";;");
        return parts.Length > 1 ? parts[1].Trim() : context.RoomId;
    }

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            return;
        }

        // Format: eventName;;roomId
        var parts = context.Target.Split(";;");
        var eventName = parts[0].Trim();
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

        await _eventRoleMappingService.DeleteMappingAsync(eventName, roomId, cancellationToken);
        context.ReplyLocalizedMessage("eventroles_deleted", eventName);

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
