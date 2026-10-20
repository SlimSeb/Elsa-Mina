using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Permissions;

[NamedCommand("grant", Aliases = ["give-rights", "giverights"])]
public class GrantCommand : Command
{
    private static readonly TimeSpan MAX_DURATION = TimeSpan.FromDays(366);
    private static readonly Rank[] GRANTABLE_RANKS = [Rank.Voiced, Rank.Driver, PermissionGrant.MAX_RANK];

    private readonly IRoomsManager _roomsManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPermissionGrantStore _permissionGrantStore;
    private readonly IPermissionTargetResolver _permissionTargetResolver;
    private readonly ICommandRegistry _commandRegistry;
    private readonly IClockService _clockService;

    public GrantCommand(IRoomsManager roomsManager,
        IAuthorizationService authorizationService,
        IPermissionGrantStore permissionGrantStore,
        IPermissionTargetResolver permissionTargetResolver,
        ICommandRegistry commandRegistry,
        IClockService clockService)
    {
        _roomsManager = roomsManager;
        _authorizationService = authorizationService;
        _permissionGrantStore = permissionGrantStore;
        _permissionTargetResolver = permissionTargetResolver;
        _commandRegistry = commandRegistry;
        _clockService = clockService;
    }

    // Un rang donné s'arrête à @ : il suffit jamais pour lancer cette commande, donc seuls les vrais # donnent des droits
    public override Rank RequiredRank => Rank.RoomOwner;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "permissions_grant_help";

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

        var target = _permissionTargetResolver.Resolve(arguments.Values[1]);
        if (target == null)
        {
            context.ReplyLocalizedMessage("permissions_unknown_target", arguments.Values[1]);
            return;
        }

        var targetLabel = PermissionsFormatter.FormatTarget(context, target.Kind, target.Value);
        var granterRank = await _authorizationService.GetEffectiveRankAsync(context, room.RoomId, cancellationToken);
        var refusalKey = GetRefusalKey(target, granterRank);
        if (refusalKey != null)
        {
            context.ReplyLocalizedMessage(refusalKey, targetLabel);
            return;
        }

        TimeSpan? duration = null;
        if (arguments.Values.Count > 2 && !string.IsNullOrWhiteSpace(arguments.Values[2]))
        {
            if (!TryParseDuration(arguments.Values[2], out var parsedDuration))
            {
                context.ReplyLocalizedMessage("permissions_invalid_duration", arguments.Values[2]);
                return;
            }

            duration = parsedDuration;
        }

        var now = _clockService.CurrentUtcDateTimeOffset;
        var grant = new PermissionGrant(room.RoomId, userId, target.Kind, target.Value, context.Sender.UserId, now,
            now + duration);
        await _permissionGrantStore.SaveAsync(grant, cancellationToken);

        context.ReplyLocalizedMessage("permissions_grant_success", arguments.Values[0], targetLabel, room.Name,
            PermissionsFormatter.FormatExpiry(context, grant.ExpiresAt, room.TimeZone));
    }

    // On peut donner seulement ce qu'on peut lancer soi-même, et jamais les commandes de droits ni celles de la whitelist
    private string GetRefusalKey(PermissionTarget target, Rank granterRank)
    {
        switch (target.Kind)
        {
            case PermissionGrantKind.Rank:
                return GRANTABLE_RANKS.Contains(User.GetRankFromCharacter(target.Value[0]))
                    ? null
                    : "permissions_rank_not_grantable";
            case PermissionGrantKind.Command:
                return GetCommandRefusalKey(_commandRegistry.Find(target.Value), granterRank);
            default:
                return GetCategoryRefusalKey(target.Value, granterRank);
        }
    }

    private string GetCommandRefusalKey(ICommand command, Rank granterRank)
    {
        if (command == null || command.IsWhitelistOnly || command.Category == Category)
        {
            return "permissions_target_not_grantable";
        }

        var requiredRank = GetHighestRequiredRank(command);
        if (requiredRank <= Rank.Regular)
        {
            return "permissions_target_already_allowed";
        }

        return requiredRank > granterRank ? "permissions_target_above_rank" : null;
    }

    private string GetCategoryRefusalKey(string category, Rank granterRank)
    {
        var commands = _commandRegistry.Commands
            .Where(command => string.Equals(command.Category, category, StringComparison.OrdinalIgnoreCase))
            .Where(command => !command.IsWhitelistOnly)
            .ToList();
        if (commands.Count == 0 || string.Equals(category, Category, StringComparison.OrdinalIgnoreCase))
        {
            return "permissions_target_not_grantable";
        }

        if (commands.Any(command => GetHighestRequiredRank(command) > granterRank))
        {
            return "permissions_target_above_rank";
        }

        return commands.All(command => GetHighestRequiredRank(command) <= Rank.Regular)
            ? "permissions_target_already_allowed"
            : null;
    }

    private static Rank GetHighestRequiredRank(ICommand command) =>
        command.RequiredRank > command.RequiredRankInPrivateMessage
            ? command.RequiredRank
            : command.RequiredRankInPrivateMessage;

    private static bool TryParseDuration(string text, out TimeSpan duration)
    {
        try
        {
            duration = text.ToTimeSpan() ?? TimeSpan.Zero;
        }
        catch (OverflowException)
        {
            duration = TimeSpan.Zero;
        }

        return duration > TimeSpan.Zero && duration <= MAX_DURATION;
    }
}
