namespace ElsaMina.Commands.Profile;

public interface IProfileService
{
    Task<ProfileViewModel> GetProfileViewModelAsync(string userId, string roomId, CancellationToken cancellationToken = default);
    Task<string> GetProfileHtmlAsync(string userId, string roomId, CancellationToken cancellationToken = default);
}
