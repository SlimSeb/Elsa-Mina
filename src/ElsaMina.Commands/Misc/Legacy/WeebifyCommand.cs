using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Legacy;

[NamedCommand("weebify", Aliases = ["weeb"])]
public class WeebifyCommand : Command
{
    private static readonly string[] SUFFIXES = ["sama", "san", "kun", "chan", "sensei", "senpai"];
    private static readonly string[] LINKS = ["-", " ", "-"];

    private readonly IRandomService _randomService;

    public WeebifyCommand(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "weebify_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var words = context.Target.Split(' ');
        var result = string.Join(' ', words.Select(word =>
            $"{word}{_randomService.RandomElement(LINKS)}{_randomService.RandomElement(SUFFIXES)}"));

        context.Reply($"Result: {result}", rankAware: true);
        return Task.CompletedTask;
    }
}