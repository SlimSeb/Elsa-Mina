using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Core.Services.Authorization;

/// <summary>
/// Un droit donné à un user dans une room par un room owner. Sans date d'expiration, il dure jusqu'à ce qu'on le
/// retire
/// </summary>
public sealed record PermissionGrant(
    string RoomId,
    string UserId,
    PermissionGrantKind Kind,
    string Target,
    string GrantedBy,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// Le plus haut rang qu'on peut donner. Les commandes de room owner restent aux vrais room owners, comme ça un
    /// rang donné permet jamais de donner des droits à son tour
    /// </summary>
    public const Rank MAX_RANK = Rank.Mod;

    public bool IsActiveAt(DateTimeOffset now) => ExpiresAt == null || ExpiresAt > now;

    /// <summary>
    /// Le rang donné par un droit de type <see cref="PermissionGrantKind.Rank"/>, plafonné à <see cref="MAX_RANK"/>.
    /// Regular pour les autres types
    /// </summary>
    public Rank GrantedRank
    {
        get
        {
            if (Kind != PermissionGrantKind.Rank || string.IsNullOrEmpty(Target))
            {
                return Rank.Regular;
            }

            var rank = User.GetRankFromCharacter(Target[0]);
            return rank > MAX_RANK ? MAX_RANK : rank;
        }
    }

    /// <summary>
    /// Vrai si le droit porte sur cette cible : un user a un seul droit de rang par room (quel que soit le rang), et
    /// un seul droit par commande ou par catégorie
    /// </summary>
    public bool IsFor(PermissionGrantKind kind, string target) =>
        Kind == kind
        && (Kind == PermissionGrantKind.Rank || string.Equals(Target, target, StringComparison.OrdinalIgnoreCase));
}
