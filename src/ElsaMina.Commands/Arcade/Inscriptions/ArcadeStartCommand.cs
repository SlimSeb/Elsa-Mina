using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Arcade.Inscriptions;

[NamedCommand("arcadestart", Aliases = ["startarcade"])]
public class ArcadeStartCommand : Command
{
    private readonly IArcadeInscriptionsManager _inscriptionsManager;

    public ArcadeStartCommand(IArcadeInscriptionsManager inscriptionsManager)
    {
        _inscriptionsManager = inscriptionsManager;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "arcade_start_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (_inscriptionsManager.HasActiveInscriptions(context.RoomId))
        {
            context.ReplyLocalizedMessage("arcade_start_already_active");
            return Task.CompletedTask;
        }

        var args = string.IsNullOrWhiteSpace(context.Target)
            ? []
            : context.Target.Split(',');

        var title = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0].Trim()
            : "Tournoi Arcade";

        if (args.Length > 1)
        {
            if (!int.TryParse(args[1].Trim(), out var timerMinutes))
            {
                context.ReplyLocalizedMessage("arcade_timer_parse_error");
                return Task.CompletedTask;
            }

            if (timerMinutes <= 0)
            {
                context.ReplyLocalizedMessage("arcade_timer_positive_integer");
                return Task.CompletedTask;
            }

            _inscriptionsManager.InitInscriptions(context.RoomId, title);
            _inscriptionsManager.StartTimer(context.RoomId, timerMinutes);
            context.ReplyLocalizedMessage("arcade_start_with_timer", title, timerMinutes);
        }
        else
        {
            _inscriptionsManager.InitInscriptions(context.RoomId, title);
            context.ReplyLocalizedMessage("arcade_start_no_timer", title);
        }

        return Task.CompletedTask;
    }
}