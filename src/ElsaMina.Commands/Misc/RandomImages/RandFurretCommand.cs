using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("randfurret")]
public class RandFurretCommand : Command
{
    private static readonly string[] FURRET_GIFS =
    [
        "https://i.imgur.com/VCtvQCC.gif",
        "https://i.imgur.com/n7US6Pj.gif",
        "https://i.imgur.com/TrA1Jto.gif",
        "https://i.imgur.com/aci704N.gif",
        "https://i.imgur.com/8RJFUUw.gif",
        "https://i.imgur.com/KHoSd5F.gif"
    ];

    private readonly IRandomService _randomService;

    public RandFurretCommand(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "randfurret_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var gifUrl = _randomService.RandomElement(FURRET_GIFS);
        context.ReplyHtml(
            $"<marquee scrollamount=\"6\"><img src=\"{gifUrl}\" width=\"160\" height=\"90\"></marquee>",
            rankAware: true);
        return Task.CompletedTask;
    }
}
