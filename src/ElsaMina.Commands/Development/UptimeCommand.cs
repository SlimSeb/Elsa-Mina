using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Development;

[NamedCommand("uptime")]
public class UptimeCommand : Command
{
    private readonly IBot _bot;

    public UptimeCommand(IBot bot)
    {
        _bot = bot;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRankInPrivateMessage => Rank.Regular;
    public override Rank RequiredRank => Rank.Voiced;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        context.ReplyLocalizedMessage("uptime", _bot.UpTime.ToString("g", context.Culture));
        
        return Task.CompletedTask;
    }
}