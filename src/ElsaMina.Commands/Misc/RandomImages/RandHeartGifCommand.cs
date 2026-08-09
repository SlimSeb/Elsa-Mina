using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("randheart")]
public class RandHeartGifCommand : Command
{
    private readonly IKlipyService _klipyService;

    public RandHeartGifCommand(IKlipyService klipyService)
    {
        _klipyService = klipyService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "randheart_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var media = await _klipyService.GetRandomMediaAsync("hearts", KlipyMediaSize.Sm,
            KlipyMediaFormat.Gif, cancellationToken);
        if (media == null)
        {
            Log.Error("Klipy returned no result for hearts query.");
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        context.ReplyHtml(
            $"<img src=\"{media.Url}\" style=\"transform:rotate(0deg);\" width=\"{media.Width}\" height=\"{media.Height}\">",
            rankAware: true);
    }
}
