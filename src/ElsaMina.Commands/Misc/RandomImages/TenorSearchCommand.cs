using System.Collections.Concurrent;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("tenorsearch", "gifs", "gifsearch", "tenor")]
public class TenorSearchCommand : Command
{
    private const int GIF_COUNT = 8;
    private const int THUMBNAIL_MAX_WIDTH = 150;
    private static readonly ConcurrentDictionary<string, DateTimeOffset> ROOM_COOLDOWNS = new();
    private static readonly ConcurrentDictionary<string, DateTimeOffset> USER_COOLDOWNS = new();

    private readonly ITenorService _tenorService;
    private readonly IConfiguration _configuration;
    private readonly ITemplatesManager _templatesManager;
    private readonly IClockService _clockService;
    private readonly IArcadeEventsService _eventsService;

    public TenorSearchCommand(ITenorService tenorService, IConfiguration configuration,
        ITemplatesManager templatesManager, IClockService clockService, IArcadeEventsService eventsService)
    {
        _tenorService = tenorService;
        _configuration = configuration;
        _templatesManager = templatesManager;
        _clockService = clockService;
        _eventsService = eventsService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => false;
    public override string HelpMessageKey => "tenorsearch_help";

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
                context.ReplyLocalizedMessage("tenorsearch_room_cooldown", remaining.Seconds);
                return;
            }
        }

        if (USER_COOLDOWNS.TryGetValue(context.Sender.UserId, out var lastUserUse))
        {
            var remaining = TenorConstants.PER_USER_COOLDOWN - (now - lastUserUse);
            if (remaining > TimeSpan.Zero)
            {
                context.ReplyLocalizedMessage("tenorsearch_user_cooldown",
                    (int)remaining.TotalMinutes, remaining.Seconds);
                return;
            }
        }

        ROOM_COOLDOWNS[context.RoomId] = now;
        USER_COOLDOWNS[context.Sender.UserId] = now;

        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var gifs = await _tenorService.GetMultipleMediaAsync(
            context.Target.Trim(), "gif", GIF_COUNT, cancellationToken);

        if (gifs == null || gifs.Count == 0)
        {
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        var thumbnails = gifs.Select(gif =>
        {
            var thumbWidth = Math.Min(gif.Width / 2, THUMBNAIL_MAX_WIDTH);
            var thumbHeight = gif.Width > 0 ? gif.Height * thumbWidth / gif.Width : thumbWidth;
            return new TenorGifThumbnail
            {
                Url = gif.Url,
                OriginalWidth = gif.Width,
                OriginalHeight = gif.Height,
                ThumbWidth = thumbWidth,
                ThumbHeight = thumbHeight
            };
        }).ToList();

        var template = await _templatesManager.GetTemplateAsync("Misc/RandomImages/TenorSearch",
            new TenorSearchViewModel
            {
                Culture = context.Culture,
                Gifs = thumbnails,
                Trigger = _configuration.Trigger
            });

        context.SendHtmlTo(context.Sender.UserId, template.RemoveNewlines());
    }
}