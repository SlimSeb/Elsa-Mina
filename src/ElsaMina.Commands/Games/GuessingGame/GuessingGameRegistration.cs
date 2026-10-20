namespace ElsaMina.Commands.Games.GuessingGame;

/// <summary>
/// Un jeu de devinette et le nom de commande qui le lance
/// </summary>
public record GuessingGameRegistration(string CommandName, Func<GuessingGame> CreateGame);
