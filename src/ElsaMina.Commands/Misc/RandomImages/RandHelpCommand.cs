using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("randhelp", Aliases = ["randlist"])]
public class RandHelpCommand : Command
{
    private static readonly string[] RAND_COMMANDS =
    [
        "randcat", "randdog", "randheart", "randimage", "randturtle", "randcapy", "randgoat",
        "randelephant", "randpig", "randbird", "randdolphin", "randwolf", "randtiger", "randcheetah",
        "randlion", "randjaguar", "randbutterfly", "randmouse", "randmonkey", "randbear", "randrabbit",
        "randfrog", "randsnake", "randspider", "randshark", "randfurret", "randraclette",
        "rand", "randgif", "randmp4", "walk"
    ];

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "randhelp_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var items = string.Join("<br>", RAND_COMMANDS.Select(name => $"- {name}"));
        context.ReplyHtml(
            $"<div style='max-height: 200px; overflow-y: auto'>Liste des commandes random images:<br>{items}</div>",
            rankAware: true);
        return Task.CompletedTask;
    }
}
