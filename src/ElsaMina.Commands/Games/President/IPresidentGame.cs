using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

public interface IPresidentGame : ILeavableCardGame, IResendableCardGame, ISubstitutableCardGame
{
    IReadOnlyList<PresidentPlayer> Players { get; }
    int PlayerCount { get; }
    PresidentPhase Phase { get; }
    PresidentPlayer CurrentPlayer { get; }
    int RoundNumber { get; }
    int TotalRounds { get; set; }

    PresidentTrick CurrentTrick { get; }
    bool IsMatchRequired { get; }
    PresidentPlayer LastTrickWinner { get; }
    IReadOnlyList<PresidentPlayer> FinishOrder { get; }

    IReadOnlyList<string> Log { get; }

    IReadOnlyList<(int Rank, int Count)> GetLegalPlays(PresidentPlayer player);
    bool CanPass(PresidentPlayer player);

    Task PlayAsync(IUser user, int rank, int count);
    Task PassAsync(IUser user);
    Task GiveAsync(IUser user, IReadOnlyList<PresidentCard> cards);
}
