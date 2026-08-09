using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("randmp4")]
public class RandMp4Command : Command
{
    private readonly IKlipyService _klipyService;

    public RandMp4Command(IKlipyService klipyService)
    {
        _klipyService = klipyService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "randmp4_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var searchTerm = string.IsNullOrWhiteSpace(context.Target)
            ? "bot"
            : context.Target.ToLowerAlphaNum();

        var media = await _klipyService.GetRandomMediaAsync(searchTerm, KlipyMediaSize.Md,
            KlipyMediaFormat.Mp4, cancellationToken);
        if (media == null)
        {
            Log.Error("Klipy returned no mp4 for query: {Query}", searchTerm);
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        context.Reply($"!show {media.Url}", rankAware: true);
    }
}
