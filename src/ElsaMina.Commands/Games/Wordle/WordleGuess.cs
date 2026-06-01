using System.Collections.Generic;

namespace ElsaMina.Commands.Games.Wordle;

public class WordleGuess
{
    public required string Word { get; init; }
    public required IReadOnlyList<WordleLetterState> States { get; init; }
}
