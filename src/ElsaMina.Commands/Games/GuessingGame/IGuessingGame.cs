using ElsaMina.Core.Services.Games;

namespace ElsaMina.Commands.Games.GuessingGame;

public interface IGuessingGame : ICancellableGame
{
    void OnAnswer(string userName, string answer);
    void StopGame();
}