namespace ElsaMina.Core.Services.Rooms.Parameters;

/// <summary>
/// A room parameter. <see cref="Identifier"/> is the short key its value is stored under and the only thing equality
/// looks at; <see cref="Name"/> is the readable name staff can also type in the room configuration command.
/// Core defines the parameters the runtime needs below; features define theirs next to the
/// <see cref="IRoomParameterProvider"/> that describes them.
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
