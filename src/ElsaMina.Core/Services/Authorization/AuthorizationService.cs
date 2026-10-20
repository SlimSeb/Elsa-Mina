using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.Authorization;

public class AuthorizationService : IAuthorizationService
{
    private readonly IRoomsManager _roomsManager;
    private readonly IUserDetailsManager _userDetailsManager;
    private readonly IPermissionGrantStore _permissionGrantStore;

    public AuthorizationService(IRoomsManager roomsManager,
        IUserDetailsManager userDetailsManager,
        IPermissionGrantStore permissionGrantStore)
    {
        _roomsManager = roomsManager;
        _userDetailsManager = userDetailsManager;
        _permissionGrantStore = permissionGrantStore;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(IContext context, ICommand command,
        CancellationToken cancellationToken = default)
    {
        var requiredRank = context.IsPrivateMessage ? command.RequiredRankInPrivateMessage : command.RequiredRank;
        if (requiredRank <= Rank.Regular || context.IsSenderWhitelisted)
        {
            return new AuthorizationResult(true, context.RoomId, requiredRank);
        }

        var roomId = ResolveTargetRoomId(context, command);
        if (HasCommandGrant(roomId, context.Sender.UserId, command))
        {
            return new AuthorizationResult(true, roomId, requiredRank);
        }

        var rank = await GetEffectiveRankAsync(context, roomId, cancellationToken);
        return new AuthorizationResult(rank >= requiredRank, roomId, requiredRank);
    }

    public async Task<Rank> GetEffectiveRankAsync(IContext context, string roomId,
        CancellationToken cancellationToken = default)
    {
        if (context.IsSenderWhitelisted)
        {
            return Rank.Admin;
        }

        var normalizedRoomId = NormalizeRoomId(roomId);
        var showdownRank = await GetShowdownRankAsync(context, normalizedRoomId, cancellationToken);
        var grantedRank = GetGrantedRank(normalizedRoomId, context.Sender.UserId);
        return showdownRank > grantedRank ? showdownRank : grantedRank;
    }

    public Rank GetGrantedRank(string roomId, string userId)
    {
        var grants = _permissionGrantStore.GetActiveGrants(roomId, userId);
        return grants.Count == 0 ? Rank.Regular : grants.Max(grant => grant.GrantedRank);
    }

    private string ResolveTargetRoomId(IContext context, ICommand command)
    {
        // Null = la commande agit dans la room du contexte, ou refusera de tourner. Une room vide reste vide : la commande
        // agirait sur elle, donc personne y a de rang
        var roomId = command.GetTargetRoomId(context);
        return roomId == null ? context.RoomId : NormalizeRoomId(roomId);
    }

    // Une room où est le bot garde son id tel quel (les groupchats ont des tirets), le reste est normalisé comme les
    // ids tapés par les users
    private string NormalizeRoomId(string roomId)
    {
        var trimmedRoomId = roomId?.Trim() ?? string.Empty;
        return _roomsManager.HasRoom(trimmedRoomId) ? trimmedRoomId : trimmedRoomId.ToLowerAlphaNum();
    }

    private bool HasCommandGrant(string roomId, string userId, ICommand command)
    {
        // Les commandes whitelist restent à la whitelist, quoi qu'on ait pu enregistrer
        if (command.IsWhitelistOnly)
        {
            return false;
        }

        return _permissionGrantStore.GetActiveGrants(roomId, userId).Any(grant =>
            grant.IsFor(PermissionGrantKind.Command, command.Name)
            || (!string.IsNullOrEmpty(command.Category) && grant.IsFor(PermissionGrantKind.Category, command.Category)));
    }

    private async Task<Rank> GetShowdownRankAsync(IContext context, string roomId,
        CancellationToken cancellationToken)
    {
        if (!context.IsPrivateMessage && context.RoomId == roomId)
        {
            return context.Sender.Rank;
        }

        // Le bot est dans la room : sa liste de users est à jour, et un user qui y est pas a pas de rang à y faire valoir.
        // Ça évite aussi d'attendre une réponse du serveur derrière le message qu'on est en train de traiter
        var room = _roomsManager.GetRoom(roomId);
        if (room != null)
        {
            return room.Users.GetValueOrDefault(context.Sender.UserId)?.Rank ?? Rank.Regular;
        }

        var userDetails = await _userDetailsManager.GetUserDetailsAsync(context.Sender.UserId, cancellationToken);
        var roomKey = userDetails?.Rooms?.Keys.FirstOrDefault(key => key.ToLowerAlphaNum() == roomId);
        return roomKey == null ? Rank.Regular : User.GetRankFromCharacter(roomKey[0]);
    }
}
