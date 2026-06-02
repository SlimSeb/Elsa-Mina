using ElsaMina.Commands.Teams.TeamProviders;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Teams.TeamPreviewOnLink;

public class DisplayTeamOnLinkHandler : ChatMessageHandler
{
    private static readonly TimeSpan PER_USER_COOLDOWN = TimeSpan.FromMinutes(15);
    private const int MAX_TEAM_SIZE = 6;

    private readonly Dictionary<string, DateTime> _lastTeamTimes = new();

    private readonly IClockService _clockService;
    private readonly ITeamLinkMatchFactory _teamLinkMatchFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public DisplayTeamOnLinkHandler(IContextFactory contextFactory,
        IClockService clockService,
        ITeamLinkMatchFactory teamLinkMatchFactory,
        ITemplatesManager templatesManager,
        IConfiguration configuration)
        : base(contextFactory)
    {
        _clockService = clockService;
        _teamLinkMatchFactory = teamLinkMatchFactory;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override async Task HandleMessageAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Message.StartsWith(_configuration.Trigger)
            || context.Message.StartsWith("/raw")
            || context.Sender.UserId == _configuration.Name.ToLowerAlphaNum())
        {
            return;
        }

        var isShowingTeamLinksPreviewEnabled =
            (await context.Room.GetParameterValueAsync(Parameter.ShowTeamLinksPreview, cancellationToken)).ToBoolean();
        if (!isShowingTeamLinksPreviewEnabled)
        {
            return;
        }

        var now = _clockService.CurrentUtcDateTime;
        if (_lastTeamTimes.TryGetValue(context.Sender.UserId, out var userLastTeamTime)
            && now - userLastTeamTime < PER_USER_COOLDOWN && !context.IsSenderWhitelisted)
        {
            return;
        }

        _lastTeamTimes[context.Sender.UserId] = now;

        var teamLinkMatch = _teamLinkMatchFactory.FindTeamLinkMatch(context.Message);
        if (teamLinkMatch == null)
        {
            return;
        }

        var sharedTeam = await teamLinkMatch.GetTeamExport(cancellationToken);
        if (sharedTeam == null)
        {
            Log.Error("An error occurred while fetching team from link {0}",
                context.Message);
            return;
        }

        var team = ShowdownTeamsUtils.DeserializeTeamExport(sharedTeam.TeamExport);
        if (team.Count > MAX_TEAM_SIZE || team.Count == 0 || team.Any(set => string.IsNullOrEmpty(set.Species)))
        {
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Teams/TeamPreview", new TeamPreviewViewModel
        {
            Author = sharedTeam.Author,
            Culture = context.Culture,
            Sender = context.Sender.Name,
            Team = team
        });

        context.ReplyHtml(template.RemoveNewlines());
    }
}