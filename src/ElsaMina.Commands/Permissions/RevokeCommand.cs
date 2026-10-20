using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Permissions;

[NamedCommand("revoke", Aliases = ["revoke-rights", "revokerights"])]
public class RevokeCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IPermissionGrantStore _permissionGrantStore;
    private readonly IPermissionTargetResolver _permissionTargetResolver;

    public RevokeCommand(IRoomsManager roomsManager,
        IPermissionGrantStore permissionGrantStore,
        IPermissionTargetResolver permissionTargetResolver)
    {
        _roomsManager = roomsManager;
        _permissionGrantStore = permissionGrantStore;
        _permissionTargetResolver = permissionTargetResolver;
    }

    public override Rank RequiredRank => Rank.RoomOwner;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "permissions_revoke_help";

    public override string GetTargetRoomId(IContext context) => PermissionArguments.Parse(context).RoomId;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var arguments = PermissionArguments.Parse(context);
        var userId = arguments.Values.Count >= 2 ? arguments.Values[0].ToLowerAlphaNum() : null;
        if (arguments.RoomId == null || string.IsNullOrEmpty(userId))
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

        var userGrants = _permissionGrantStore.GetActiveGrants(room.RoomId, userId);
        var grant = FindGrant(userGrants, arguments.Values[1]);
        if (grant == null)
        {
            context.ReplyLocalizedMessage("permissions_revoke_not_found", arguments.Values[0], arguments.Values[1],
                room.Name);
            return;
        }

        await _permissionGrantStore.RemoveAsync(room.RoomId, userId, grant.Kind, grant.Target, cancellationToken);
        context.ReplyLocalizedMessage("permissions_revoke_success", arguments.Values[0],
            PermissionsFormatter.FormatTarget(context, grant.Kind, grant.Target), room.Name);
    }

    // Le droit tel qu'on le donne (un rang retire le droit de rang, quel qu'il soit), ou tel qu'il est enregistré,
    // pour pouvoir retirer le droit d'une commande qui existe plus
    private PermissionGrant FindGrant(IReadOnlyList<PermissionGrant> userGrants, string text)
    {
        var target = _permissionTargetResolver.Resolve(text);
        if (target != null)
        {
            return userGrants.FirstOrDefault(grant => grant.IsFor(target.Kind, target.Value));
        }

        return userGrants.FirstOrDefault(grant =>
            string.Equals(grant.Target, text.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
