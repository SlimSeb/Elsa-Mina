using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// A card game that shows each player their hand on a private HTML page, which they can ask for again
/// if they closed it. Poker uses private chat panels instead and has nothing to resend.
/// </summary>
public interface IResendableCardGame : ICardGame
{
    Task ResendPlayerPageAsync(IUser user);
}
