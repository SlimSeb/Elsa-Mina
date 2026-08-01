using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Development;

[NamedCommand("say")]
public class SayCommand : DevelopmentCommand
{
    private readonly IBot _bot;

    public SayCommand(IBot bot)
    {
        _bot = bot;
    }

    public override bool IsPrivateMessageOnly => true;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(';', 2);
        if (parts.Length < 2)
        {
            context.Reply("Usage: say <roomId>;<message>");
            return Task.CompletedTask;
        }

        Log.Information("Say command used: {0}", context.Target);
        _bot.Say(parts[0], parts[1]);
        return Task.CompletedTask;
    }
}