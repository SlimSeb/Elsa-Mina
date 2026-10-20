namespace ElsaMina.Commands.Permissions;

public interface IPermissionTargetResolver
{
    /// <summary>
    /// Lit le droit tapé par un user : un rang (<c>+</c>, <c>%</c>, <c>@</c> ou leur nom), une commande (nom ou alias),
    /// ou une catégorie (<c>category:Badges</c>, ou juste <c>Badges</c> si aucune commande s'appelle comme ça).
    /// Null si c'est rien de tout ça
    /// </summary>
    PermissionTarget Resolve(string text);
}
