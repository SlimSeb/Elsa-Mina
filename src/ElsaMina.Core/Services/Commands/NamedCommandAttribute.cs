namespace ElsaMina.Core.Services.Commands;

[AttributeUsage(AttributeTargets.Class)]
public class NamedCommandAttribute : Attribute
{
    public NamedCommandAttribute(string name)
    {
        Name = name;
    }

    public NamedCommandAttribute(string name, params string[] aliases)
    {
        Name = name;
        Aliases = aliases;
    }

    public string Name { get; }

    public string[] Aliases { get; set; } = [];

    /// <summary>
    /// La feature de la commande, utilisée pour les switchs de features et la liste des commandes. Si pas renseignée,
    /// on la déduit du namespace : le segment après <c>ElsaMina.Commands.</c>, ou le nom du projet pour les commandes
    /// définies dans un autre projet de feature (<c>ElsaMina.Battles.Commands</c> => <c>Battles</c>)
    /// </summary>
    public string Category { get; set; }
}