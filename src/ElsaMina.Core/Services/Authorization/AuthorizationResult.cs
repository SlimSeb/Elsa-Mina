using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Core.Services.Authorization;

/// <summary>
/// Le résultat de la vérif de rang d'une commande : la room où on a vérifié et le rang qu'il fallait, pour pouvoir
/// dire au user ce qui lui manque
/// </summary>
public sealed record AuthorizationResult(bool IsAllowed, string RoomId, Rank RequiredRank);
