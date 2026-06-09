using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Ai.Chat;

[NamedCommand("personality", "persona", "mood", "setpersonality")]
public class SetPersonalityCommand : Command
{
    private readonly IPersonalityService _personalityService;

    public SetPersonalityCommand(IPersonalityService personalityService)
    {
        _personalityService = personalityService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "personality_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var target = context.Target?.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            var current = _personalityService.GetPersonality(context.RoomId);
            context.ReplyLocalizedMessage("personality_current",
                BotPersonalities.GetLabel(current), BotPersonalities.AvailableNames);
            return Task.CompletedTask;
        }

        if (!BotPersonalities.TryParse(target, out var personality))
        {
            context.ReplyLocalizedMessage("personality_invalid", target, BotPersonalities.AvailableNames);
            return Task.CompletedTask;
        }

        _personalityService.SetPersonality(context.RoomId, personality);
        context.ReplyLocalizedMessage("personality_set", BotPersonalities.GetLabel(personality));
        return Task.CompletedTask;
    }
}
