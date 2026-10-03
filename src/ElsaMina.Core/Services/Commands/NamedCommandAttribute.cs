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
    /// The feature the command belongs to, used for feature switches and the command list. When not set, it is
    /// derived from the namespace: the segment after <c>ElsaMina.Commands.</c>, or the project name for commands
    /// defined in another feature project (<c>ElsaMina.Battles.Commands</c> gives <c>Battles</c>).
    /// </summary>
    public string Category { get; set; }
}