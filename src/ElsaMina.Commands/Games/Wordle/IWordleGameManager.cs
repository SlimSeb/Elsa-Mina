namespace ElsaMina.Commands.Games.Wordle;

public interface IWordleGameManager
{
    IWordleGame GetGame(string roomId, string userId);
    void RegisterGame(string roomId, string userId, IWordleGame game);
    void RemoveGame(string roomId, string userId);
}
