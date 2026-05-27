using ElsaMina.Commands.Games.GuessingGame;
using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Commands.Tournaments.Handlers;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Development;

[NamedCommand("templates", Aliases = ["templates-debug", "templatedebug"])]
public class TemplatesDebugCommand : DevelopmentCommand
{
    private readonly ITemplatesManager _templatesManager;

    public TemplatesDebugCommand(ITemplatesManager templatesManager)
    {
        _templatesManager = templatesManager;
    }
    
    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",");
        var templateName = parts[0].Trim();
        LocalizableViewModel model = templateName switch
        {
            "Profile/Profile" => new ProfileViewModel
            {
                UserId = parts[1],
                UserName = parts[2],
                Avatar = parts[3],
                Culture = context.Culture
            },
            "GuessingGame/GuessingGameResult" => new GuessingGameResultViewModel
            {
                Culture = context.Culture,
                Scores = new Dictionary<string, int>
                {
                    ["speks"] = 14,
                    ["morsay"] = 12,
                    ["thylane"] = 7,
                    ["lionyx"] = 1
                }
            },
            "Tournaments/Betting/BettingAnnouncement" => new BettingAnnouncementViewModel
            {
                Culture = context.Culture,
                BotName = "ElsaMina",
                Trigger = "-",
                RoomId = "testroom",
                IsBettingOpen = true,
                SecondsToClose = 120,
                Players =
                ((string[])[
                    "speks", "morsay", "thylane", "lionyx", "awa", "piratilla", "flutes",
                    "bluxio", "simioth", "nagham", "turtlek", "leafywind", "kazuki"
                ]).Select(p => new TournamentPlayer(p, p)).ToArray(),
                BetsByPlayer = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["speks"] = ["awa", "piratilla", "nagham"],
                    ["morsay"] = ["thylane", "bluxio"],
                    ["lionyx"] = ["simioth"],
                    ["flutes"] = ["zozo", "shinzoabe"],
                    ["turtlek"] = ["kazuki", "morsay", "leafywind", "cortexovitch", "concerto"]
                }
            },
            _ => null
        };
        
        var template = await _templatesManager.GetTemplateAsync(templateName, model);
        context.ReplyHtmlPage($"debug-template-{templateName}", template.RemoveNewlines());
    }
}