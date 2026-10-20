using ElsaMina.Core.Services.Authorization;

namespace ElsaMina.Commands.Permissions;

/// <summary>
/// Ce sur quoi porte un droit, tel qu'il est stocké : le symbole d'un rang, le nom principal d'une commande ou le nom
/// d'une catégorie
/// </summary>
public sealed record PermissionTarget(PermissionGrantKind Kind, string Value);
