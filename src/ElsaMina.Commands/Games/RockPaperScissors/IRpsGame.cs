using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;

namespace ElsaMina.Commands.Games.RockPaperScissors;

public interface IRpsGame : IGame
{
    IContext Context { get; set; }
    IReadOnlyList<string> Players { get; }
    Task<(bool Success, string MessageKey, object[] Args)> Join(string userName);
    Task Play(string userId, RpsChoice choice);
    void Cancel();
}
