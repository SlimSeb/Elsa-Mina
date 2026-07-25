using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Closes the lobby and deals the first hand. The game decides whether enough players have joined.
/// </summary>
public abstract class BeginGameCommand<TGame> : GameLifecycleCommandBase<TGame> where TGame : class, ICardGame
{
    public override Rank RequiredRank => Rank.Voiced;

    protected override Task ExecuteAsync(IContext context, TGame game) => game.StartAsync(context.Sender);
}
