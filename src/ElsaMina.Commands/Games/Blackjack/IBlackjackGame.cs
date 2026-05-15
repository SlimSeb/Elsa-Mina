using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Blackjack;

public interface IBlackjackGame : IGame
{
    BlackjackHand PlayerHand { get; }
    BlackjackHand DealerHand { get; }
    BlackjackGameState State { get; }
    bool IsPrivateMode { get; set; }
    string TargetRoomId { get; set; }
    string TargetUserId { get; set; }
    IContext Context { get; set; }
    IUser Owner { get; set; }

    Task DisplayAnnounce();
    Task StartGame();
    Task Hit(IUser user);
    Task Stand(IUser user);
    Task CancelAsync();
}
