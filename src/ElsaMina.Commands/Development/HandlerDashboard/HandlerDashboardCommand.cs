using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Development.HandlerDashboard;

[NamedCommand("handlerdashboard", Aliases = ["hdash"])]
public class HandlerDashboardCommand : DevelopmentCommand
{
    private const string PAGE_NAME = "handlerdashboard";
    private const string TOGGLE_PREFIX = "toggle ";

    private readonly IHandlerManager _handlerManager;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public HandlerDashboardCommand(IHandlerManager handlerManager,
        ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _handlerManager = handlerManager;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var target = context.Target.Trim();
        if (target.StartsWith(TOGGLE_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            var identifier = target[TOGGLE_PREFIX.Length..].Trim();
            var handler = _handlerManager.Handlers
                .FirstOrDefault(handler => handler.Identifier == identifier);
            if (handler != null)
            {
                handler.IsEnabled = !handler.IsEnabled;
            }
        }

        var lines = _handlerManager.Handlers
            .OrderBy(handler => handler.Identifier)
            .Select(handler => new HandlerLineModel
            {
                Identifier = handler.Identifier,
                Name = GetShortName(handler.Identifier),
                IsEnabled = handler.IsEnabled
            })
            .ToList();

        var viewModel = new HandlerDashboardViewModel
        {
            Culture = context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            Handlers = lines
        };

        var template = await _templatesManager.GetTemplateAsync(
            "Development/HandlerDashboard/HandlerDashboard", viewModel);
        context.ReplyHtmlPage(PAGE_NAME, template.RemoveNewlines());
    }

    private static string GetShortName(string identifier)
    {
        var lastDot = identifier.LastIndexOf('.');
        return lastDot >= 0 ? identifier[(lastDot + 1)..] : identifier;
    }
}
