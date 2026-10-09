using ElsaMina.Commands.Repeats;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Repeats.List;

[NamedCommand("repeatslist",
    Aliases = ["repeats", "showrepeats", "show-repeats", "repeatslist", "repeatlist", "repeat-list", "repeats-list"])]
public class RepeatsListCommand : Command
{
    private readonly IRepeatsManager _repeatsManager;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public RepeatsListCommand(IRepeatsManager repeatsManager, ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _repeatsManager = repeatsManager;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Driver;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var repeats = _repeatsManager.GetRepeats(context.RoomId)?.ToList();
        if (repeats == null || repeats.Count == 0)
        {
            context.ReplyLocalizedMessage("aboutrepeat_not_found");
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Repeats/List/RepeatsList", new RepeatsListViewModel
        {
            Culture = context.Culture,
            Repeats = repeats,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger
        });

        context.ReplyHtmlPage($"repeats-{context.RoomId}", template.RemoveNewlines());
    }
}