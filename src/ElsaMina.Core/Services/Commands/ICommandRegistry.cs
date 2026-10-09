namespace ElsaMina.Core.Services.Commands;

public interface ICommandRegistry
{
    /// <summary>
    /// Toutes les commandes enregistrées, une fois chacune
    /// </summary>
    IReadOnlyCollection<ICommand> Commands { get; }

    /// <summary>
    /// Trouve une commande par son nom ou un de ses alias, null sinon
    /// </summary>
    ICommand Find(string nameOrAlias);
}
