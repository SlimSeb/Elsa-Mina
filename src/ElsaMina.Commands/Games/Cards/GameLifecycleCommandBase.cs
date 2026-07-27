using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Base for the card game commands that only make sense in the room the game runs in: joining,
/// quitting, starting the deal and calling it off. Each game supplies its resource prefix so the
/// shared flow can emit that game's own strings.
/// </summary>
/// <typeparam name="TGame">The game interface the room's running game must implement.</typeparam>
public abstract class GameLifecycleCommandBase<TGame> : Command where TGame : class, ICardGame
{
    /// <summary>
    /// The prefix every resource key of this game starts with, e.g. <c>"tarot"</c>.
    /// </summary>
    protected abstract string ResourcePrefix { get; }

    public override Rank RequiredRank => Rank.Regular;

    protected string Key(string suffix) => $"{ResourcePrefix}_{suffix}";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not TGame game)
        {
            context.ReplyLocalizedMessage(Key("not_running"));
            return;
        }

        await ExecuteAsync(context, game);
    }

    protected abstract Task ExecuteAsync(IContext context, TGame game);
}
