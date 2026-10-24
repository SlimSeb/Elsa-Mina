namespace ElsaMina.Core.Services.UserData;

public interface IUserDataService
{
    Task<UserDataDto> GetUserData(string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="GetUserData"/>, without the cache: for what changes, such as ladder ratings.
    /// </summary>
    Task<UserDataDto> GetLatestUserDataAsync(string userName, CancellationToken cancellationToken = default);
    Task<DateTimeOffset> GetRegisterDateAsync(string userName, CancellationToken cancellationToken = default);
}