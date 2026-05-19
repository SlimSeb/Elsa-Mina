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
    private readonly ITenorService _tenorService;

    public RandMp4Command(ITenorService tenorService)
    {
        _tenorService = tenorService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "randmp4_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var searchTerm = string.IsNullOrWhiteSpace(context.Target)
            ? "bot"
            : context.Target.ToLowerAlphaNum();

        var media = await _tenorService.GetRandomMediaAsync(searchTerm, "mp4", cancellationToken);
        if (media == null)
        {
            Log.Error("Tenor returned no mp4 for query: {Query}", searchTerm);
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        context.Reply($"!show {media.Url}", rankAware: true);
    }
}
