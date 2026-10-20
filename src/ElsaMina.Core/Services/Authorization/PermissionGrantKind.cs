namespace ElsaMina.Core.Services.Authorization;

/// <summary>
/// Ce que donne un droit. Stocké par nom en bdd : ne jamais renommer une valeur
/// </summary>
public enum PermissionGrantKind
{
    /// <summary>
    /// Le bot traite le user comme s'il avait ce rang dans la room (cible : le symbole du rang, genre <c>%</c>)
    /// </summary>
    Rank,

    /// <summary>
    /// Le user peut lancer une commande (cible : le nom principal de la commande)
    /// </summary>
    Command,

    /// <summary>
    /// Le user peut lancer toutes les commandes d'une catégorie (cible : le nom de la catégorie)
    /// </summary>
    Category
}
