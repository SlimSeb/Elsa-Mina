namespace ElsaMina.Commands.Games.Tcg.Decks;

/// <summary>
/// Outcome of validating a deck for legality. When <see cref="IsValid"/> is <c>false</c>,
/// <see cref="ErrorKey"/> is the localization key explaining why (with <see cref="Args"/>).
/// </summary>
public sealed record TcgDeckValidationResult(bool IsValid, string ErrorKey = null, object[] Args = null)
{
    public static readonly TcgDeckValidationResult Valid = new(true);

    public static TcgDeckValidationResult Invalid(string errorKey, params object[] args) =>
        new(false, errorKey, args);
}
