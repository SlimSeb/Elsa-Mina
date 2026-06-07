namespace ElsaMina.Commands.Games.Tcg.Cards;

/// <summary>
/// Presentation helpers for rendering a <see cref="TcgType"/> in HTML panels (colors and a short
/// symbol). Kept separate from <see cref="TcgTypes"/> so the data layer stays free of styling.
/// </summary>
public static class TcgTypeStyle
{
    public static string Color(TcgType type) => type switch
    {
        TcgType.Grass => "#3fa34d",
        TcgType.Fire => "#e8553a",
        TcgType.Water => "#3a8ee8",
        TcgType.Lightning => "#e8c13a",
        TcgType.Psychic => "#a45cc8",
        TcgType.Fighting => "#c8743a",
        _ => "#9aa0a6"
    };

    public static string Symbol(TcgType type) => type switch
    {
        TcgType.Grass => "G",
        TcgType.Fire => "R",
        TcgType.Water => "W",
        TcgType.Lightning => "L",
        TcgType.Psychic => "P",
        TcgType.Fighting => "F",
        _ => "C"
    };
}
