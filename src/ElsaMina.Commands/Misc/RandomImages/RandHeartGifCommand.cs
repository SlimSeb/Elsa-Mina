using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("randheart")]
public class RandHeartGifCommand : Command
{
    private readonly ITenorService _tenorService;

    public RandHeartGifCommand(ITenorService tenorService)
    {
        _tenorService = tenorService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "randheart_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var media = await _tenorService.GetRandomMediaAsync("hearts", "tinygifpreview", cancellationToken);
        if (media == null)
        {
            Log.Error("Tenor returned no result for hearts query.");
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        context.ReplyHtml(
            $"<img src=\"{media.Url}\" style=\"transform:rotate(0deg);\" width=\"{media.Width}\" height=\"{media.Height}\">",
            rankAware: true);
    }
}
