namespace ElsaMina.Commands.Games.Blackjack;

public interface IBlackjackGameManager
{
    IBlackjackGame GetGame(string roomId, string userId);
    void RegisterGame(string roomId, string userId, IBlackjackGame game);
    void RemoveGame(string roomId, string userId);
}
