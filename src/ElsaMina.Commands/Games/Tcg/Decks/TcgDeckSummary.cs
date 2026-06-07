namespace ElsaMina.Commands.Games.Tcg.Decks;

/// <summary>
/// A lightweight summary of a saved deck, used by the deck list panel.
/// </summary>
public sealed record TcgDeckSummary(string Name, int CardCount, bool IsLegal);
