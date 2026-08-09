using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("rand")]
public class RandCommand : Command
{
    private readonly IKlipyService _klipyService;

    public RandCommand(IKlipyService klipyService)
    {
        _klipyService = klipyService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "rand_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var searchTerm = string.IsNullOrWhiteSpace(context.Target)
            ? "bot"
            : context.Target.ToLowerAlphaNum();

        var media = await _klipyService.GetRandomMediaAsync(searchTerm, KlipyMediaSize.Sm,
            KlipyMediaFormat.Gif, cancellationToken);
        if (media == null)
        {
            Log.Error("Klipy returned no result for query: {Query}", searchTerm);
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        context.ReplyHtml(
            $"<img src=\"{media.Url}\" style=\"transform:rotate(0deg);\" width=\"{media.Width}\" height=\"{media.Height}\">",
            rankAware: true);
    }
}
