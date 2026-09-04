namespace ElsaMina.Core.Services.Games;

public interface ICancellableGame : IGame
{
    Task CancelAsync();
}
