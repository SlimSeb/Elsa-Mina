using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Battleship;

public class BattleshipModel : LocalizableViewModel
{
    public IBattleshipGame CurrentGame { get; init; }
    public BattleshipPlayer Viewer { get; init; }
    public BattleshipPlayer Opponent { get; init; }
    public string Trigger { get; init; }
    public string BotName { get; init; }
    public string RoomId { get; init; }
}
