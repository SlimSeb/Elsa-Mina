using System.Globalization;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.UserDetails;

namespace ElsaMina.Core.Contexts;

public class PmContext : Context
{
    public PmContext(IConfiguration configuration,
        IResourcesService resourcesService,
        IRoomsManager roomsManager,
        IUserDetailsManager userDetailsManager,
        IBot bot,
        string message,
        string target,
        IUser sender,
        string command) : base(configuration, resourcesService, roomsManager, userDetailsManager,
        bot, message, target, sender, command)
    {
        Culture = DefaultCulture;
        RoomId = DefaultRoom;
    }

    public override string RoomId { get; }

    public override bool IsPrivateMessage => true;

    public sealed override CultureInfo Culture { get; set; }

    public override ContextType Type => ContextType.Pm;

    public override bool HasRankOrHigher(Rank requiredRank) => true;

    protected override Task<bool> IsAllowingErrorMessagesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public override void Reply(string message, bool rankAware = false)
        => Bot.Send($"|/pm {Sender.UserId}, {message}");

    public override void ReplyHtml(string html, string roomId = null, bool rankAware = false)
        => Bot.Say(roomId ?? RoomId, $"/pminfobox {Sender.UserId}, {html}");

    public override void SendHtmlTo(string userId, string html, string roomId = null) =>
        Bot.Say(roomId ?? RoomId, $"/pminfobox {userId}, {html}");

    public override void SendUpdatableHtml(string htmlId, string html, bool isChanging)
    {
        var command = isChanging ? "pmuhtmlchange" : "pmuhtml";
        Bot.Say(RoomId, $"/{command} {Sender.UserId}, {htmlId}, {html}");
    }

    public override void SendPrivateUpdatableHtml(string userId, string roomId, string htmlId, string html, bool isChanging)
    {
        var command = isChanging ? "changeprivateuhtml" : "sendprivateuhtml";
        Bot.Say(roomId ?? RoomId, $"/{command} {userId}, {htmlId}, {html}");
    }
}
