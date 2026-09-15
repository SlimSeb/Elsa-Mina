using System.Globalization;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Contexts;

public class RoomContext : Context
{
    private readonly IRoom _room;

    public RoomContext(ContextDependencies dependencies,
        string message,
        string target,
        IUser sender,
        string command,
        IRoom room,
        long timestamp) : base(dependencies, message, target, sender, command)
    {
        _room = room;
        Timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp);
    }

    public override string RoomId => _room.RoomId;

    public override bool IsPrivateMessage => false;

    public override CultureInfo Culture
    {
        get => _room.Culture;
        set => _room.Culture = value;
    }

    public override ContextType Type => ContextType.Room;

    public DateTimeOffset Timestamp { get; }

    protected override async Task<bool> IsAllowingErrorMessagesAsync(CancellationToken cancellationToken = default)
    {
        var parameterValue = await _room.GetParameterValueAsync(Parameter.ShowErrorMessages, cancellationToken);
        return parameterValue.ToBoolean();
    }

    public override bool HasRankOrHigher(Rank requiredRank)
    {
        return IsSenderWhitelisted || Sender.Rank >= requiredRank;
    }

    public override void Reply(string message, bool rankAware = false)
    {
        if (rankAware && !HasRankOrHigher(Rank.Voiced))
        {
            Bot.Say(RoomId, $"/pm {Sender.UserId}, {message}");
            return;
        }

        Bot.Say(RoomId, message);
    }

    public override void ReplyHtml(string html, string roomId = null, bool rankAware = false)
    {
        if (rankAware && !HasRankOrHigher(Rank.Voiced))
        {
            Bot.Say(RoomId, $"/pminfobox {Sender.UserId}, {html}");
            return;
        }

        Bot.Say(RoomId, $"/addhtmlbox {html}");
    }

    public override void SendHtmlTo(string userId, string html, string roomId = null)
    {
        Bot.Say(RoomId, $"/sendprivatehtmlbox {userId}, {html}");
    }

    public override void SendUpdatableHtml(string htmlId, string html, bool isChanging)
    {
        var command = isChanging ? "changeuhtml" : "adduhtml";
        Bot.Say(RoomId, $"/{command} {htmlId}, {html}");
    }

    public override void SendPrivateUpdatableHtml(string userId, string roomId, string htmlId, string html, bool isChanging)
    {
        var command = isChanging ? "changeprivateuhtml" : "sendprivateuhtml";
        Bot.Say(roomId ?? RoomId, $"/{command} {userId}, {htmlId}, {html}");
    }
}
