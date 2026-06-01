using System.Globalization;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Core.Contexts;

public interface IContext
{
    string Message { get; }
    string RawMessage { get; }
    string Target { get; }
    IUser Sender { get; }
    IRoom Room { get; }
    string Command { get; }
    bool IsSenderWhitelisted { get; }
    string RoomId { get; }
    bool IsPrivateMessage { get; }
    CultureInfo Culture { get; set; }
    ContextType Type { get; }

    string GetString(string key);
    string GetString(string key, params object[] formatArguments);
    void ReplyHtmlPage(string pageName, string html);
    void SendHtmlPageTo(string userId, string pageName, string html);
    bool HasRankOrHigher(Rank requiredRank);
    Task<Rank> GetUserRankInRoom(string roomId, CancellationToken cancellationToken = default);
    Task<bool> HasSufficientRankInRoom(string roomId, Rank requiredRank, CancellationToken cancellationToken = default);

    void Reply(string message, bool rankAware = false);
    void SendMessageIn(string roomId, string message);
    void ReplyLocalizedMessage(string key, params object[] formatArguments);
    void ReplyRankAwareLocalizedMessage(string key, params object[] formatArguments);
    void ReplyHtml(string html, string roomId = null, bool rankAware = false);
    void SendHtmlTo(string userId, string html, string roomId = null);
    void SendUpdatableHtml(string htmlId, string html, bool isChanging);
    void SendPrivateUpdatableHtml(string userId, string roomId, string htmlId, string html, bool isChanging);
    Task HandleErrorAsync(Exception exception, CancellationToken cancellationToken = default);
}
