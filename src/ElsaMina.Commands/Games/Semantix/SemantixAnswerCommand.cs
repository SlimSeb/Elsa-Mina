using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Semantix;

/// <summary>
/// Debug command for developers: shows today's Semantix answer.
/// </summary>
[NamedCommand("sxanswer", Aliases = ["sxword"])]
public class SemantixAnswerCommand : Command
{
    private readonly ISemantixDailyService _dailyService;

    public SemantixAnswerCommand(ISemantixDailyService dailyService)
    {
        _dailyService = dailyService;
    }

    public override bool IsWhitelistOnly => true;
    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsHidden => true;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var answer = _dailyService.GetDailyAnswer();
        context.Reply($"/pm {context.Sender.UserId}, Semantix ({_dailyService.Today:yyyy-MM-dd}): {answer}");
        return Task.CompletedTask;
    }
}
