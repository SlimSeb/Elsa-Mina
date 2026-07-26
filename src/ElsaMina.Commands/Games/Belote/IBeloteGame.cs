using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

public interface IBeloteGame : IResendableCardGame, ISubstitutableCardGame
{
    IReadOnlyList<BelotePlayer> Players { get; }
    int PlayerCount { get; }
    BelotePhase Phase { get; }
    int BiddingRound { get; }
    BelotePlayer CurrentPlayer { get; }
    BelotePlayer Taker { get; }
    BeloteCard TurnedCard { get; }
    Suit? Trump { get; }

    BeloteTrick CurrentTrick { get; }
    BeloteTrick LastTrick { get; }
    BelotePlayer LastTrickWinner { get; }
    BeloteCard LastPlayedCard { get; }
    int TrickNumber { get; }
    int TotalTricks { get; }

    int Team0Tricks { get; }
    int Team1Tricks { get; }

    BeloteScoreResult ScoreResult { get; }

    Task BidAsync(IUser user, bool pass, Suit? chosenSuit);
    Task PlayAsync(IUser user, BeloteCard card);
    IReadOnlyCollection<BeloteCard> GetLegalMoves(BelotePlayer player);
}
