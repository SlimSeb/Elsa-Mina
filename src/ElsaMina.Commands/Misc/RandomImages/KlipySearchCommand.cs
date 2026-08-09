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

[NamedCommand("klipysearch", "gifs", "gifsearch", "klipy")]
public class KlipySearchCommand : Command
{
    private const int GIF_COUNT = 8;
    private const int THUMBNAIL_MAX_WIDTH = 150;

    private readonly IKlipyService _klipyService;
    private readonly IConfiguration _configuration;
    private readonly ITemplatesManager _templatesManager;
    private readonly IClockService _clockService;
    private readonly IArcadeEventsService _eventsService;
    private readonly IGifCooldownService _cooldownService;

    public KlipySearchCommand(IKlipyService klipyService, IConfiguration configuration,
        ITemplatesManager templatesManager, IClockService clockService, IArcadeEventsService eventsService,
        IGifCooldownService cooldownService)
    {
        _klipyService = klipyService;
        _configuration = configuration;
        _templatesManager = templatesManager;
        _clockService = clockService;
        _eventsService = eventsService;
        _cooldownService = cooldownService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => false;
    public override string HelpMessageKey => "klipysearch_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var isEnabled = (await context.Room.GetParameterValueAsync(Parameter.KlipyGifEnabled,
            cancellationToken)).ToBoolean();
        if (!isEnabled)
        {
            return;
        }

        if (_eventsService.AreGamesMuted(context.RoomId))
        {
            context.ReplyLocalizedMessage("klipygif_muted_for_events");
            return;
        }

        var now = _clockService.CurrentUtcDateTimeOffset;
        var (roomRemaining, userRemaining) =
            _cooldownService.GetRemainingCooldowns(context.RoomId, context.Sender.UserId, now);
        if (!context.IsSenderWhitelisted && (roomRemaining > TimeSpan.Zero || userRemaining > TimeSpan.Zero))
        {
            if (roomRemaining >= userRemaining)
            {
                context.ReplyLocalizedMessage("klipysearch_room_cooldown", (int)roomRemaining.TotalSeconds);
            }
            else
            {
                context.ReplyLocalizedMessage("klipysearch_user_cooldown",
                    (int)userRemaining.TotalMinutes, userRemaining.Seconds);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var gifs = await _klipyService.SearchAsync(context.Target.Trim(), GIF_COUNT, cancellationToken);

        if (gifs == null || gifs.Count == 0)
        {
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        var thumbnails = gifs.Select(gif =>
        {
            var thumbWidth = Math.Min(gif.Preview.Width, THUMBNAIL_MAX_WIDTH);
            var thumbHeight = gif.Preview.Width > 0
                ? gif.Preview.Height * thumbWidth / gif.Preview.Width
                : thumbWidth;
            return new KlipyGifThumbnail
            {
                PreviewUrl = gif.Preview.Url,
                FullUrl = gif.Full.Url,
                FullWidth = gif.Full.Width,
                FullHeight = gif.Full.Height,
                ThumbWidth = thumbWidth,
                ThumbHeight = thumbHeight
            };
        }).ToList();

        var template = await _templatesManager.GetTemplateAsync("Misc/RandomImages/KlipySearch",
            new KlipySearchViewModel
            {
                Culture = context.Culture,
                Gifs = thumbnails,
                Trigger = _configuration.Trigger
            });

        context.SendHtmlTo(context.Sender.UserId, template.RemoveNewlines());
    }
}
