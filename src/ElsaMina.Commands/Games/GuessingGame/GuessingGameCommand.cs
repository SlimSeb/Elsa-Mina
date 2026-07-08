using ElsaMina.Commands.Games.GuessingGame.Capitals;
using ElsaMina.Commands.Games.GuessingGame.Countries;
using ElsaMina.Commands.Games.GuessingGame.Gatekeepers;
using ElsaMina.Commands.Games.GuessingGame.HigherLower;
using ElsaMina.Commands.Games.GuessingGame.PokeCries;
using ElsaMina.Commands.Games.GuessingGame.PokeDesc;
using ElsaMina.Commands.Games.GuessingGame.WhosThatPokemon;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.GuessingGame;

[NamedCommand("guessinggame", Aliases = ["countriesgame", "pokedesc", "pokecries", "gatekeepers", "capitalcities", "higherlower", "whosthatpokemon"])]
public class GuessingGameCommand : Command
{
    private const int MAX_TURNS_COUNT = 20;

    private readonly IDependencyContainerService _dependencyContainerService;
    private readonly IArcadeEventsService _arcadeEventsService;

    public GuessingGameCommand(IDependencyContainerService dependencyContainerService,
        IArcadeEventsService arcadeEventsService)
    {
        _dependencyContainerService = dependencyContainerService;
        _arcadeEventsService = arcadeEventsService;
    }

    public override Rank RequiredRank => Rank.Voiced;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (_arcadeEventsService.AreGamesMuted(context.RoomId))
        {
            context.ReplyLocalizedMessage("games_muted_event");
            return;
        }

        if (!int.TryParse(context.Target, out var turnsCount))
        {
            context.ReplyLocalizedMessage("guessing_game_specify");
            return;
        }

        if (turnsCount is <= 0 or > MAX_TURNS_COUNT)
        {
            context.ReplyLocalizedMessage("guessing_game_invalid_number_turns", MAX_TURNS_COUNT);
            return;
        }

        var room = context.Room;
        if (room.Game != null)
        {
            context.ReplyLocalizedMessage("guessing_game_currently_ongoing");
            return;
        }

        GuessingGame game = context.Command switch
        {
            "countriesgame" => _dependencyContainerService.Resolve<CountriesGame>(),
            "pokedesc" => _dependencyContainerService.Resolve<PokeDescGame>(),
            "pokecries" => _dependencyContainerService.Resolve<PokeCriesGame>(),
            "gatekeepers" => _dependencyContainerService.Resolve<GatekeepersGame>(),
            "capitalcities" => _dependencyContainerService.Resolve<CapitalCitiesGame>(),
            "higherlower" => _dependencyContainerService.Resolve<HigherLowerGame>(),
            "whosthatpokemon" => _dependencyContainerService.Resolve<WhosThatPokemonGame>(),
            _ => null
        };
        if (game == null)
        {
            context.ReplyLocalizedMessage("guessing_game_invalid_command");
            return;
        }

        game.TurnsCount = turnsCount;
        game.Context = context;

        room.Game = game;
        await game.Start();
    }
}