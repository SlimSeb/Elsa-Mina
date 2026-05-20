namespace ElsaMina.Commands.Users.Colors;

public interface INameColorsService
{
    Task SetColorAsync(string userId, string color, CancellationToken cancellationToken = default);
    Task<bool> DeleteColorAsync(string userId, CancellationToken cancellationToken = default);
}
