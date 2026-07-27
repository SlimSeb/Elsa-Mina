using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Calls the running game off, whatever state it is in.
/// </summary>
public abstract class EndGameCommand<TGame> : GameLifecycleCommandBase<TGame> where TGame : class, ICardGame
{
    public override Rank RequiredRank => Rank.Voiced;

    protected override async Task ExecuteAsync(IContext context, TGame game)
    {
        await game.CancelAsync();
        context.ReplyLocalizedMessage(Key("game_cancelled"));
    }
}
