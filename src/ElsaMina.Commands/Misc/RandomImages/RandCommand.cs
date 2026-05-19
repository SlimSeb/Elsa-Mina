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
    private readonly ITenorService _tenorService;

    public RandCommand(ITenorService tenorService)
    {
        _tenorService = tenorService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "rand_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var searchTerm = string.IsNullOrWhiteSpace(context.Target)
            ? "bot"
            : context.Target.ToLowerAlphaNum();

        var media = await _tenorService.GetRandomMediaAsync(searchTerm, "tinygifpreview", cancellationToken);
        if (media == null)
        {
            Log.Error("Tenor returned no result for query: {Query}", searchTerm);
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        context.ReplyHtml(
            $"<img src=\"{media.Url}\" style=\"transform:rotate(0deg);\" width=\"{media.Width}\" height=\"{media.Height}\">",
            rankAware: true);
    }
}
