using System.Globalization;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Contexts;

public abstract class Context : IContext
{
    private readonly IConfiguration _configuration;
    private readonly IResourcesService _resourcesService;
    private readonly IRoomsManager _roomsManager;
    private readonly IUserDetailsManager _userDetailsManager;

    protected Context(IConfiguration configuration,
        IResourcesService resourcesService,
        IRoomsManager roomsManager,
        IUserDetailsManager userDetailsManager,
        IBot bot,
        string message,
        string target,
        IUser sender,
        string command)
    {
        _configuration = configuration;
        _resourcesService = resourcesService;
        _roomsManager = roomsManager;
        _userDetailsManager = userDetailsManager;

        Bot = bot;
        Message = message;
        Target = target;
        Sender = sender;
        Command = command;
    }

    public IBot Bot { get; }
    public string Message { get; }
    public string RawMessage { get; init; }
    public string Target { get; }
    public IUser Sender { get; }
    public IRoom Room => _roomsManager.GetRoom(RoomId);
    public string Command { get; }
    public bool IsSenderWhitelisted => _configuration.Whitelist.Contains(Sender.UserId);

    protected string DefaultRoom => _configuration.DefaultRoom;
    protected CultureInfo DefaultCulture => new(_configuration.DefaultLocaleCode);

    public void ReplyHtmlPage(string pageName, string html)
    {
        Bot.Say(_configuration.DefaultRoom, $"/sendhtmlpage {Sender.UserId}, {pageName}, {html}");
    }

    public void SendMessageIn(string roomId, string message)
    {
        Bot.Say(roomId, message);
    }

    public void SendHtmlPageTo(string userId, string pageName, string html)
    {
        Bot.Say(_configuration.DefaultRoom, $"/sendhtmlpage {userId}, {pageName}, {html}");
    }

    public void CloseHtmlPage(string userId, string pageName)
    {
        Bot.Say(_configuration.DefaultRoom, $"/closehtmlpage {userId}, {pageName}");
    }

    public string GetString(string key)
    {
        var localizedString = _resourcesService.GetString(key, Culture ?? DefaultCulture);
        return string.IsNullOrWhiteSpace(localizedString) ? key : localizedString;
    }

    public string GetString(string key, params object[] formatArguments)
    {
        if (formatArguments == null || formatArguments.Length == 0)
        {
            return GetString(key);
        }
        return string.Format(GetString(key), formatArguments);
    }

    public void ReplyLocalizedMessage(string key, params object[] formatArguments)
    {
        Reply(GetString(key, formatArguments));
    }

    public void ReplyRankAwareLocalizedMessage(string key, params object[] formatArguments)
    {
        Reply(GetString(key, formatArguments), rankAware: true);
    }

    public async Task HandleErrorAsync(Exception exception, CancellationToken cancellationToken = default)
    {
        var isAllowed = await IsAllowingErrorMessagesAsync(cancellationToken);
        if (!isAllowed)
        {
            return;
        }

        ReplyLocalizedMessage("command_execution_error");
        if (!string.IsNullOrWhiteSpace(_configuration.GithubToken) &&
            !string.IsNullOrWhiteSpace(_configuration.GithubRepository))
        {
            ReplyLocalizedMessage("command_execution_report_bug_command", _configuration.Trigger);
        }
        else if (!string.IsNullOrWhiteSpace(_configuration.BugReportLink))
        {
            ReplyLocalizedMessage("command_execution_report_bug", _configuration.BugReportLink);
        }
        Reply($"!code {exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}");
    }

    public Task<Rank> GetUserRankInRoom(string roomId, CancellationToken cancellationToken = default)
    {
        return GetUserRankInRoomAsync(roomId, Sender.UserId, cancellationToken);
    }

    public async Task<bool> HasSufficientRankInRoom(string roomId, Rank requiredRank,
        CancellationToken cancellationToken = default)
    {
        if (IsSenderWhitelisted)
        {
            return true;
        }

        var rank = await GetUserRankInRoom(roomId, cancellationToken);
        return rank >= requiredRank;
    }

    private async Task<Rank> GetUserRankInRoomAsync(string roomId, string userId, CancellationToken cancellationToken)
    {
        var userDetails = await _userDetailsManager.GetUserDetailsAsync(userId, cancellationToken);
        if (userDetails == null)
        {
            return Rank.Regular;
        }

        var room = userDetails.Rooms.Keys.FirstOrDefault(key => key.ToLowerAlphaNum() == roomId);
        return room == null ? Rank.Regular : User.GetRankFromCharacter(room[0]);
    }

    public abstract string RoomId { get; }
    public abstract bool IsPrivateMessage { get; }
    public abstract CultureInfo Culture { get; set; }
    public abstract ContextType Type { get; }
    protected abstract Task<bool> IsAllowingErrorMessagesAsync(CancellationToken cancellationToken = default);

    public abstract bool HasRankOrHigher(Rank requiredRank);
    public abstract void Reply(string message, bool rankAware = false);
    public abstract void ReplyHtml(string html, string roomId = null, bool rankAware = false);
    public abstract void SendHtmlTo(string userId, string html, string roomId = null);
    public abstract void SendUpdatableHtml(string htmlId, string html, bool isChanging);
    public abstract void SendPrivateUpdatableHtml(string userId, string roomId, string htmlId, string html, bool isChanging);
}
