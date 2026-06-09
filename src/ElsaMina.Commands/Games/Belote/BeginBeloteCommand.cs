using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

[NamedCommand("belotestart", Aliases = ["belotebegin"])]
public class BeginBeloteCommand : Command
{
    public override Rank RequiredRank => Rank.Voiced;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IBeloteGame game)
        {
            context.ReplyLocalizedMessage("belote_not_running");
            return;
        }

        await game.StartAsync(context.Sender);
    }
}
