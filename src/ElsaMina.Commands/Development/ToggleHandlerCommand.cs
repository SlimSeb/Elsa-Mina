using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Development;

[NamedCommand("togglehandler", Aliases = ["th"])]
public class ToggleHandlerCommand : DevelopmentCommand
{
    private readonly IHandlerManager _handlerManager;

    public override string HelpMessageKey => "togglehandler_help";

    public ToggleHandlerCommand(IHandlerManager handlerManager)
    {
        _handlerManager = handlerManager;
    }

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var target = context.Target.Trim();
        if (string.IsNullOrEmpty(target))
        {
            var lines = _handlerManager.Handlers
                .OrderBy(handler => handler.Identifier)
                .Select(handler => $"{(handler.IsEnabled ? "Y" : "N")} {GetShortName(handler)}");
            context.ReplyHtml(
                $"<strong>{context.GetString("togglehandler_list_title")}</strong><br />{string.Join("<br />", lines)}",
                rankAware: true);
            return Task.CompletedTask;
        }

        var matches = _handlerManager.Handlers
            .Where(handler => GetShortName(handler).Equals(target, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            context.ReplyLocalizedMessage("togglehandler_not_found", target);
            return Task.CompletedTask;
        }

        var targetHandler = matches[0];
        targetHandler.IsEnabled = !targetHandler.IsEnabled;
        context.ReplyLocalizedMessage(
            targetHandler.IsEnabled ? "togglehandler_enabled" : "togglehandler_disabled",
            GetShortName(targetHandler));
        return Task.CompletedTask;
    }

    private static string GetShortName(IHandler handler)
    {
        var identifier = handler.Identifier;
        var lastDot = identifier.LastIndexOf('.');
        return lastDot >= 0 ? identifier[(lastDot + 1)..] : identifier;
    }
}
