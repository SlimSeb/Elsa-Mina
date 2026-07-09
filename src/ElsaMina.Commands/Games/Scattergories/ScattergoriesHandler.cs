using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers.DefaultHandlers;

namespace ElsaMina.Commands.Games.Scattergories;

public class ScattergoriesHandler : ChatMessageHandler
{
    public ScattergoriesHandler(IContextFactory contextFactory)
        : base(contextFactory)
    {
    }

    public override Task HandleMessageAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is IScattergoriesGame scattergories)
        {
            scattergories.OnAnswer(context.Sender.Name, context.Message);
        }

        return Task.CompletedTask;
    }
}
