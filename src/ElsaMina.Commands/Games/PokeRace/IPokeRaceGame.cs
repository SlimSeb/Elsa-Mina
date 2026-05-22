using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;

namespace ElsaMina.Commands.Games.PokeRace;

public interface IPokeRaceGame : IGame
{
    int GameId { get; }
    IContext Context { get; set; }
    IReadOnlyDictionary<string, (string Name, string Pokemon)> Players { get; }
    Task BeginJoinPhaseAsync();
    Task<(bool Success, string MessageKey, object[] Args)> JoinRaceAsync(string userName, string pokemonName);
    Task StartRaceAsync();
    void Cancel();
}