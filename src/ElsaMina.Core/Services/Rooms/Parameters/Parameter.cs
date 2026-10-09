namespace ElsaMina.Core.Services.Rooms.Parameters;

/// <summary>
/// Un paramètre de room. <see cref="Identifier"/> c'est la clé courte sous laquelle la valeur est stockée, et c'est
/// la seule chose regardée pour l'égalité ; <see cref="Name"/> c'est le nom lisible que le staff peut aussi taper
/// dans la commande de config de room. Core définit en dessous les paramètres dont le runtime a besoin, les features
/// définissent les leurs à côté de leur <see cref="IRoomParameterProvider"/>
/// </summary>
public sealed class Parameter : IEquatable<Parameter>
{
    public static readonly Parameter Locale = new("loc", nameof(Locale));
    public static readonly Parameter TimeZone = new("tzn", nameof(TimeZone));
    public static readonly Parameter HasCommandAutoCorrect = new("atc", nameof(HasCommandAutoCorrect));
    public static readonly Parameter ShowErrorMessages = new("err", nameof(ShowErrorMessages));

    public Parameter(string identifier, string name = null)
    {
        Identifier = identifier;
        Name = name ?? identifier;
    }

    public string Identifier { get; }
    public string Name { get; }

    public bool Equals(Parameter other) => other is not null && Identifier == other.Identifier;

    public override bool Equals(object obj) => Equals(obj as Parameter);

    public override int GetHashCode() => Identifier?.GetHashCode() ?? 0;

    public override string ToString() => Name;
}
