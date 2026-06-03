namespace ElsaMina.Commands.Games.Semantix;

public interface ISemantixGameManager
{
    ISemantixGame GetGame(string roomId, string userId);
    void RegisterGame(string roomId, string userId, ISemantixGame game);
    void RemoveGame(string roomId, string userId);
}
