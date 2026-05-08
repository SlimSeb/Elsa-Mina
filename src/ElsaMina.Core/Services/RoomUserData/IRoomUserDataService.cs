using ElsaMina.DataAccess.Models;

namespace ElsaMina.Core.Services.RoomUserData;

public interface IRoomUserDataService
{
    IReadOnlyDictionary<Tuple<string, string>, string> JoinPhrases { get; }
    Task InitializeJoinPhrasesAsync(CancellationToken cancellationToken = default);
    Task<RoomUser> GetOrCreateRoomSpecificUserDataAsync(string roomId, string userId, CancellationToken cancellationToken = default);

    Task GiveBadgeToUserAsync(string roomId, string userId, string badgeId,
        CancellationToken cancellationToken = default);

    Task TakeBadgeFromUserAsync(string roomId, string userId, string badgeId,
        CancellationToken cancellationToken = default);

    Task SetUserTitleAsync(string roomId, string userId, string title, CancellationToken cancellationToken = default);
    Task SetUserAvatarAsync(string roomId, string userId, string avatar, CancellationToken cancellationToken = default);

    Task SetUserEmojiAsync(string roomId, string userId, string emoji, CancellationToken cancellationToken = default);

    Task SetUserBackgroundColorAsync(string roomId, string userId, string color, CancellationToken cancellationToken = default);

    Task SetUserJoinPhraseAsync(string roomId, string userId, string joinPhrase,
        CancellationToken cancellationToken = default);
}