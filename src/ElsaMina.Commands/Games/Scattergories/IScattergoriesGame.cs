using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;

namespace ElsaMina.Commands.Games.Scattergories;

public interface IScattergoriesGame : IGame
{
    IContext Context { get; set; }
    Task StartAsync();
    void OnAnswer(string userName, string answer);
    void Cancel();
}
