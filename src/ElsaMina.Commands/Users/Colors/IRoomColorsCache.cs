namespace ElsaMina.Commands.Users.Colors;

public interface IRoomColorsCache
{
    string GetColor(string userId);
    Task LoadAsync(CancellationToken cancellationToken = default);
}
