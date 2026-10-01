using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.PokeRace;

[NamedCommand("pokerace", Aliases = ["coursepokerace"])]
public class StartPokeRaceCommand : GameCommand
{
    private readonly Func<PokeRaceGame> _gameFactory;

    public StartPokeRaceCommand(Func<PokeRaceGame> gameFactory)
    {
        _gameFactory = gameFactory;
    }

    public override Rank RequiredRank => Rank.Driver;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room.Game is IPokeRaceGame)
        {
            context.ReplyLocalizedMessage("pokerace_already_running");
            return;
        }

        if (context.Room.Game is not null)
        {
            context.ReplyLocalizedMessage("pokerace_other_game_running");
            return;
        }

        var game = _gameFactory();
        game.Context = context;
        context.Room.Game = game;
        await game.BeginJoinPhaseAsync();
    }
}