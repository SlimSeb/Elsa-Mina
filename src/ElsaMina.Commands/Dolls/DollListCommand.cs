using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Dolls;

[NamedCommand("dolllist", Aliases = ["doll-list", "dolls", "poupees"])]
public class DollListCommand : Command
{
    private readonly IDollService _dollService;
    private readonly ITemplatesManager _templatesManager;

    public DollListCommand(IDollService dollService, ITemplatesManager templatesManager)
    {
        _dollService = dollService;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "doll_list_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, Doll> catalogue;
        try
        {
            catalogue = await _dollService.GetCatalogueAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not read the doll catalogue");
            context.ReplyLocalizedMessage("doll_catalogue_unavailable", exception.Message);
            return;
        }

        var dollsBySize = catalogue.Values
            .GroupBy(doll => doll.Size)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.OrderBy(doll => doll.Name).ToList());

        if (dollsBySize.Count == 0)
        {
            context.ReplyLocalizedMessage("doll_list_empty");
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Dolls/DollList", new DollListViewModel
        {
            Culture = context.Culture,
            DollsBySize = dollsBySize
        });
        context.ReplyHtml(template.RemoveNewlines().RemoveWhitespacesBetweenTags().CollapseAttributeWhitespace(),
            rankAware: true);
    }
}
