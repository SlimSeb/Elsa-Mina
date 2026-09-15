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

[NamedCommand("klipygif")]
public class KlipyGifCommand : Command
{
    private const string KLIPY_CDN_HOST = "static.klipy.com";

    /// <summary>
    /// KLIPY serves several size tiers off the same host, so the image is scaled to fit rather than
    /// blindly halved: a sm gif (~220px) stays as-is while a pasted md or hd link gets scaled down.
    /// </summary>
    private const int MAX_DISPLAY_WIDTH = 250;

    private readonly IImageService _imageService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IClockService _clockService;
    private readonly IArcadeEventsService _eventsService;
    private readonly IGifCooldownService _cooldownService;

    public KlipyGifCommand(IImageService imageService, ITemplatesManager templatesManager, IClockService clockService,
        IArcadeEventsService eventsService, IGifCooldownService cooldownService)
    {
        _imageService = imageService;
        _templatesManager = templatesManager;
        _clockService = clockService;
        _eventsService = eventsService;
        _cooldownService = cooldownService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "klipygif_help";

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
            var reply = GetCooldownMessage(context, roomRemaining, userRemaining);
            context.Reply($"/pm {context.Sender.UserId}, {reply}");
            return;
        }

        var target = context.Target?.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var (url, width, height) = ParseTarget(target);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(KLIPY_CDN_HOST, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme != "https")
        {
            context.ReplyLocalizedMessage("klipygif_invalid_url");
            return;
        }

        if (width <= 0 || height <= 0)
        {
            (width, height) = await _imageService.GetRemoteImageDimensions(url, cancellationToken);
        }

        var (displayWidth, displayHeight) = ScaleToMaxWidth(width, height);

        var template = await _templatesManager.GetTemplateAsync("Misc/RandomImages/KlipyGif",
            new KlipyGifViewModel
            {
                Culture = context.Culture,
                Url = url,
                Width = displayWidth,
                Height = displayHeight
            });

        context.ReplyHtml(template.RemoveNewlines());
        _cooldownService.SetCooldown(context.RoomId, context.Sender.UserId, now);
    }

    private static string GetCooldownMessage(IContext context, TimeSpan roomRemaining, TimeSpan userRemaining)
    {
        if (roomRemaining >= userRemaining)
        {
            return context.GetString("klipygif_room_cooldown", (int)roomRemaining.TotalSeconds);
        }

        return context.GetString("klipygif_user_cooldown",
            (int)userRemaining.TotalMinutes, userRemaining.Seconds);
    }

    /// <summary>
    /// Splits a "url|width|height" target. When the dimensions are missing or invalid, the whole
    /// target is the url and the dimensions are 0 so that they get fetched from the image itself.
    /// </summary>
    private static (string Url, int Width, int Height) ParseTarget(string target)
    {
        var separatorIndex = target.LastIndexOf('|');
        if (separatorIndex <= 0)
        {
            return (target, 0, 0);
        }

        var dimensionPart = target[(separatorIndex + 1)..];
        var urlAndWidth = target[..separatorIndex];
        var widthSeparator = urlAndWidth.LastIndexOf('|');

        if (widthSeparator > 0
            && int.TryParse(urlAndWidth[(widthSeparator + 1)..], out var width)
            && int.TryParse(dimensionPart, out var height))
        {
            return (urlAndWidth[..widthSeparator], width, height);
        }

        return (target, 0, 0);
    }

    private static (int Width, int Height) ScaleToMaxWidth(int width, int height)
    {
        if (width <= MAX_DISPLAY_WIDTH)
        {
            return (width, height);
        }

        return (MAX_DISPLAY_WIDTH, height * MAX_DISPLAY_WIDTH / width);
    }
}
