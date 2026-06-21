using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Chess;

public class ChessModel : LocalizableViewModel
{
    public IChessGame CurrentGame { get; init; }
    public string Trigger { get; init; }
    public string BotName { get; init; }
    public string RoomId { get; init; }

    /// <summary>
    /// The color the board is rendered for. When set, the board is oriented so this color's pieces sit
    /// at the bottom and the squares become clickable on that color's turn. <c>null</c> renders the
    /// public, spectator-only board from White's perspective with no interactive buttons.
    /// </summary>
    public ChessColor? ViewerColor { get; init; }
}
