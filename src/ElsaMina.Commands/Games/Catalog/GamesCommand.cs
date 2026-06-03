using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Catalog;

[NamedCommand("games", Aliases = ["gamelist", "gameslist", "jeux"])]
public class GamesCommand : Command
{
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public GamesCommand(ITemplatesManager templatesManager, IConfiguration configuration)
    {
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "games_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var template = await _templatesManager.GetTemplateAsync("Games/Catalog/Games",
            new GamesViewModel
            {
                Culture = context.Culture,
                Trigger = _configuration.Trigger,
                Games = GamesCatalog.Games
            });

        var html = template.RemoveNewlines().CollapseAttributeWhitespace();

        // Staff in a room broadcast the list to everyone; otherwise (PM or a regular
        // user) send a full private page, which is far more readable than a small infobox.
        if (!context.IsPrivateMessage && context.HasRankOrHigher(Rank.Voiced))
        {
            context.ReplyHtml(html);
        }
        else
        {
            context.ReplyHtmlPage("games", html);
        }
    }
}
