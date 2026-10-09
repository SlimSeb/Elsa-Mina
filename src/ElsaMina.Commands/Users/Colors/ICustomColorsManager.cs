namespace ElsaMina.Commands.Users.Colors;

public interface ICustomColorsManager
{
    IReadOnlyDictionary<string, string> CustomColorsMapping { get; }

    Task FetchCustomColorsAsync(CancellationToken cancellationToken = default);
}