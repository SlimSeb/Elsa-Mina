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
    void CloseHtmlPage(string userId, string pageName);

    /// <summary>
    /// Vrai si le rang effectif du user dans la room du message (son rang Showdown, ou le rang qu'on lui a donné) est
    /// suffisant. Sert à choisir comment répondre (en room ou en privé) et pour les vérifs à l'intérieur d'une commande.
    /// Toujours vrai en PM, où tout reste privé : c'est <see cref="Services.Authorization.IAuthorizationService"/> qui
    /// décide si une commande peut être lancée
    /// </summary>
    bool HasRankOrHigher(Rank requiredRank);

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
