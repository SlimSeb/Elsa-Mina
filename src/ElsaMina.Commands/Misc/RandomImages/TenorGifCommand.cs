using System.Collections.Concurrent;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("tenorgif")]
public class TenorGifCommand : Command
{
    private const string TENOR_CDN_HOST = "media.tenor.com";
    private static readonly ConcurrentDictionary<string, DateTimeOffset> ROOM_COOLDOWNS = new();
    private static readonly ConcurrentDictionary<string, DateTimeOffset> USER_COOLDOWNS = new();

    private readonly IImageService _imageService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IClockService _clockService;
    private readonly IArcadeEventsService _eventsService;

    public TenorGifCommand(IImageService imageService, ITemplatesManager templatesManager, IClockService clockService,
        IArcadeEventsService eventsService)
    {
        _imageService = imageService;
        _templatesManager = templatesManager;
        _clockService = clockService;
        _eventsService = eventsService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "tenorgif_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var isEnabled = (await context.Room.GetParameterValueAsync(Parameter.TenorGifEnabled,
            cancellationToken)).ToBoolean();
        if (!isEnabled)
        {
            return;
        }

        if (_eventsService.AreGamesMuted(context.RoomId))
        {
            context.ReplyLocalizedMessage("tenorgif_muted_for_events");
            return;
        }

        var now = _clockService.CurrentUtcDateTimeOffset;
        if (ROOM_COOLDOWNS.TryGetValue(context.RoomId, out var lastRoomUse))
        {
            var remaining = TenorConstants.PER_ROOM_COOLDOWN - (now - lastRoomUse);
            if (remaining > TimeSpan.Zero)
            {
                var reply = context.GetString("tenorgif_room_cooldown", remaining.Seconds);
                context.Reply($"/pm {context.Sender.UserId}, {reply}");
                return;
            }
        }

        if (USER_COOLDOWNS.TryGetValue(context.Sender.UserId, out var lastUserUse))
        {
            var remaining = TenorConstants.PER_USER_COOLDOWN - (now - lastUserUse);
            if (remaining > TimeSpan.Zero)
            {
                var reply = context.GetString("tenorgif_user_cooldown",
                    (int)remaining.TotalMinutes, remaining.Seconds);
                context.Reply($"/pm {context.Sender.UserId}, {reply}");
                return;
            }
        }

        ROOM_COOLDOWNS[context.RoomId] = now;
        USER_COOLDOWNS[context.Sender.UserId] = now;

        var target = context.Target?.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var separatorIndex = target.LastIndexOf('|');
        string url;
        int width = 0, height = 0;

        if (separatorIndex > 0)
        {
            var dimensionPart = target[(separatorIndex + 1)..];
            var urlAndWidth = target[..separatorIndex];
            var widthSeparator = urlAndWidth.LastIndexOf('|');

            if (widthSeparator > 0
                && int.TryParse(urlAndWidth[(widthSeparator + 1)..], out width)
                && int.TryParse(dimensionPart, out height))
            {
                url = urlAndWidth[..widthSeparator];
            }
            else
            {
                url = target;
            }
        }
        else
        {
            url = target;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(TENOR_CDN_HOST, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme != "https")
        {
            context.ReplyLocalizedMessage("tenorgif_invalid_url");
            return;
        }

        if (width <= 0 || height <= 0)
        {
            (width, height) = await _imageService.GetRemoteImageDimensions(url, cancellationToken);
        }

        var template = await _templatesManager.GetTemplateAsync("Misc/RandomImages/TenorGif",
            new TenorGifViewModel
            {
                Culture = context.Culture,
                Url = url,
                Width = width / 2,
                Height = height / 2
            });

        context.ReplyHtml(template.RemoveNewlines());
    }
}