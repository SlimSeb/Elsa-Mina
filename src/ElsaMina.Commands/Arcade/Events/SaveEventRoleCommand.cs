using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Arcade.Events;

[NamedCommand("saveeventrole")]
public class SaveEventRoleCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IEventRoleMappingService _eventRoleMappingService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public SaveEventRoleCommand(IRoomsManager roomsManager,
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

    // Format : roomId;;eventName;;discordRoleId
    public override string GetTargetRoomId(IContext context) => context.Target.Split(";;")[0].Trim();

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            context.ReplyLocalizedMessage("eventroles_missing_data");
            return;
        }

        // Format: roomId;;eventName;;discordRoleId
        var parts = context.Target.Split(";;");
        if (parts.Length < 3)
        {
            context.ReplyLocalizedMessage("eventroles_invalid_format");
            return;
        }

        var roomId = GetTargetRoomId(context);
        var eventName = parts[1].Trim();
        var discordRoleId = parts[2].Trim();

        if (string.IsNullOrEmpty(eventName))
        {
            context.ReplyLocalizedMessage("eventroles_name_required");
            return;
        }

        if (string.IsNullOrEmpty(discordRoleId))
        {
            context.ReplyLocalizedMessage("eventroles_roleid_required");
            return;
        }

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

        var mapping = new EventRoleMapping
        {
            EventName = eventName,
            RoomId = roomId,
            DiscordRoleId = discordRoleId
        };

        await _eventRoleMappingService.SaveMappingAsync(mapping, cancellationToken);
        context.ReplyLocalizedMessage("eventroles_saved", eventName);

        await RefreshDashboard(context, roomId, cancellationToken);
    }

    private async Task RefreshDashboard(IContext context, string roomId, CancellationToken cancellationToken)
    {
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
