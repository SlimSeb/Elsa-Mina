using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Wordle;

public interface IWordleGame : IGame
{
    IReadOnlyList<WordleGuess> Guesses { get; }
    int MaxGuesses { get; }
    int WordLength { get; }
    bool IsRoundActive { get; }
    bool IsWon { get; }
    string Answer { get; }
    string RevealedAnswer { get; }
    string CurrentInput { get; }
    IReadOnlyDictionary<char, WordleLetterState> KeyboardStates { get; }
    bool IsPrivateMode { get; set; }
    string TargetRoomId { get; set; }
    string TargetUserId { get; set; }
    IContext Context { get; set; }
    IUser Owner { get; set; }

    Task StartNewRound();
    Task ResumeAsync();
    Task<WordleGuessOutcome> SubmitGuess(IUser user, string word);
    Task AppendLetter(IUser user, char letter);
    Task RemoveLetter(IUser user);
    Task SubmitCurrentInput(IUser user);
    Task CancelAsync();
}
