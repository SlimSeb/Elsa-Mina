namespace ElsaMina.Commands.Games.GuessingGame;

public interface IGuessingGameFactory
{
    /// <summary>
    /// Crée le jeu de devinette lancé par <paramref name="commandName"/>, ou null si le nom est inconnu
    /// </summary>
    GuessingGame Create(string commandName);
}
