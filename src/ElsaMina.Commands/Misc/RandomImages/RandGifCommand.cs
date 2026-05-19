using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("randgif")]
public class RandGifCommand : Command
{
    private readonly ITenorService _tenorService;

    public RandGifCommand(ITenorService tenorService)
    {
        _tenorService = tenorService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "randgif_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var searchTerm = string.IsNullOrWhiteSpace(context.Target)
            ? "bot"
            : context.Target.ToLowerAlphaNum();

        var media = await _tenorService.GetRandomMediaAsync(searchTerm, "gif", cancellationToken);
        if (media == null)
        {
            Log.Error("Tenor returned no gif for query: {Query}", searchTerm);
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        context.ReplyHtml(
            $"<img src=\"{media.Url}\" style=\"transform:rotate(0deg);\" width=\"{media.Width / 2}\" height=\"{media.Height / 2}\">",
            rankAware: true);
    }
}
