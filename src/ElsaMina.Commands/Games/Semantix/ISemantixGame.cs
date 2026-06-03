using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Semantix;

public interface ISemantixGame : IGame
{
    bool IsEnded { get; }
    bool IsRoundActive { get; }
    bool IsWon { get; }
    string Answer { get; }
    IReadOnlyList<SemantixGuess> Guesses { get; }
    SemantixGuess LastGuess { get; }
    bool IsPrivateMode { get; set; }
    string TargetRoomId { get; set; }
    string TargetUserId { get; set; }
    IContext Context { get; set; }
    IUser Owner { get; set; }

    Task<bool> StartNewRound();
    Task ResumeAsync();
    Task<SemantixGuessOutcome> SubmitGuess(IUser user, string word);
    Task CancelAsync();
}
