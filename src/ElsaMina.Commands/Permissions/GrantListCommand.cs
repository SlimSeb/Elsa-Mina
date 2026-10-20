using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Permissions;

[NamedCommand("grants", Aliases = ["grant-list", "grantlist", "rights"])]
public class GrantListCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IPermissionGrantStore _permissionGrantStore;
    private readonly ITemplatesManager _templatesManager;

    public GrantListCommand(IRoomsManager roomsManager,
        IPermissionGrantStore permissionGrantStore,
        ITemplatesManager templatesManager)
    {
        _roomsManager = roomsManager;
        _permissionGrantStore = permissionGrantStore;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "permissions_list_help";

    public override string GetTargetRoomId(IContext context) => PermissionArguments.Parse(context).RoomId;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var arguments = PermissionArguments.Parse(context);
        if (arguments.RoomId == null)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var room = _roomsManager.GetRoom(arguments.RoomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("permissions_room_not_found", arguments.RoomId);
            return;
        }

        if (context.IsPrivateMessage)
        {
            context.Culture = room.Culture;
        }

        var userId = arguments.Values.Count > 0 ? arguments.Values[0].ToLowerAlphaNum() : string.Empty;
        var grants = _permissionGrantStore.GetActiveGrants(room.RoomId)
            .Where(grant => string.IsNullOrEmpty(userId) || grant.UserId == userId)
            .OrderBy(grant => grant.UserId)
            .ThenBy(grant => grant.Kind)
            .ThenBy(grant => grant.Target)
            .ToList();
        if (grants.Count == 0)
        {
            context.ReplyLocalizedMessage("permissions_list_empty", room.Name);
            return;
        }

        var viewModel = new GrantListViewModel
        {
            Culture = context.Culture,
            RoomName = room.Name,
            Rows = grants.Select(grant => new GrantListRow
            {
                UserId = grant.UserId,
                Right = PermissionsFormatter.FormatTarget(context, grant.Kind, grant.Target),
                GrantedBy = grant.GrantedBy,
                Expires = PermissionsFormatter.FormatExpiry(context, grant.ExpiresAt, room.TimeZone)
            }).ToList()
        };
        var template = await _templatesManager.GetTemplateAsync("Permissions/GrantList", viewModel);

        // La liste reste privée : elle dit qui peut faire quoi dans la room
        context.SendHtmlTo(context.Sender.UserId, template.RemoveNewlines());
    }
}
