namespace ElsaMina.Commands.Games.Wordle;

public static class WordleConstants
{
    public const int WORD_LENGTH = 5;
    public const int MAX_GUESSES = 6;
    public static readonly TimeSpan INACTIVITY_TIMEOUT = TimeSpan.FromMinutes(3);
}
