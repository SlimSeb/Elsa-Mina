using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Core.Services.Authorization;

/// <summary>
/// Le seul endroit qui décide si un user peut lancer une commande. Dans l'ordre : la whitelist passe toujours, puis
/// un droit donné sur la commande ou sa catégorie, puis le rang effectif (le rang Showdown, ou le rang donné s'il est
/// plus haut)
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Vérifie le rang du user dans la room de <see cref="ICommand.GetTargetRoomId"/> : <see cref="ICommand.RequiredRank"/>
    /// en room, <see cref="ICommand.RequiredRankInPrivateMessage"/> en PM. Les autres restrictions (PM, whitelist,
    /// features, rooms) sont vérifiées par l'executor
    /// </summary>
    Task<AuthorizationResult> AuthorizeAsync(IContext context, ICommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Le rang que le bot reconnaît au user dans la room : son rang Showdown, ou le rang qu'on lui a donné s'il est
    /// plus haut. <see cref="Rank.Admin"/> pour un user whitelisté
    /// </summary>
    Task<Rank> GetEffectiveRankAsync(IContext context, string roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Le rang donné au user dans la room par un droit encore valide, Regular s'il en a pas. Synchrone, pour les
    /// vérifs faites pendant le traitement d'un message
    /// </summary>
    Rank GetGrantedRank(string roomId, string userId);
}
