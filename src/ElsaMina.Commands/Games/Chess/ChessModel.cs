using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Chess;

public class ChessModel : LocalizableViewModel
{
    public IChessGame CurrentGame { get; init; }
    public string Trigger { get; init; }
    public string BotName { get; init; }
    public string RoomId { get; init; }
}
