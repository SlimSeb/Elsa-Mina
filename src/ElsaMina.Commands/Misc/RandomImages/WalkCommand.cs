using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("walk")]
public class WalkCommand : Command
{
    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "walk_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var imageUrl = context.Target.Trim();
        if (string.IsNullOrEmpty(imageUrl))
        {
            context.ReplyLocalizedMessage("walk_missing_url");
            return Task.CompletedTask;
        }

        context.ReplyHtml(
            $"<marquee scrollamount=\"6\"><img src=\"{imageUrl}\" width=\"160\" height=\"90\"></marquee>",
            rankAware: true);
        return Task.CompletedTask;
    }
}
