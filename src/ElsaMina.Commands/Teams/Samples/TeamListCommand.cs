using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Teams.Samples;

[NamedCommand("team-list", Aliases = ["teams"])]
public class TeamListCommand : Command
{
    private readonly ITemplatesManager _templatesManager;
    private readonly IBotDbContextFactory _dbContextFactory;

    public TeamListCommand(ITemplatesManager templatesManager, IBotDbContextFactory dbContextFactory)
    {
        _templatesManager = templatesManager;
        _dbContextFactory = dbContextFactory;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "team_list_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        List<Team> teams;
        string roomId;
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!string.IsNullOrEmpty(context.Target))
        {
            var arguments = context.Target.Split(",");
            var format = arguments[0].ToLowerAlphaNum();
            roomId = arguments.Length >= 2 ? arguments[1].ToLowerAlphaNum() : context.RoomId;
            teams = await dbContext.Teams
                .Include(team => team.Rooms)
                .Where(team => team.Rooms != null && team.Rooms.Any(roomTeam => roomTeam.RoomId == roomId) &&
                               team.Format == format)
                .ToListAsync(cancellationToken);
        }
        else
        {
            roomId = context.RoomId;
            teams = await dbContext.Teams
                .Include(team => team.Rooms)
                .Where(team => team.Rooms != null && team.Rooms.Any(roomTeam => roomTeam.RoomId == roomId))
                .ToListAsync(cancellationToken);
        }

        if (teams.Count == 0)
        {
            context.ReplyLocalizedMessage("team_list_empty");
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Teams/TeamList", new TeamListViewModel
        {
            Culture = context.Culture,
            Teams = teams,
            TimeZone = context.Room?.TimeZone ?? TimeZoneInfo.Local
        });

        var html = template.RemoveNewlines().RemoveWhitespacesBetweenTags();
        if (context.HasRankOrHigher(Rank.Voiced))
        {
            var message = $"""<div style="overflow-y: auto; max-height: 230px;">{html}</div>""";
            context.ReplyHtml(message, roomId: roomId);
        }
        else
        {
            context.ReplyHtmlPage($"teams-{context.RoomId}", html);
        }
    }
}