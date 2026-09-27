using ElsaMina.Commands.Teams;
using ElsaMina.Core;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Tournaments.Handlers;

public class DisplayTeamsOnTourHandler : Handler
{
    private readonly ITemplatesManager _templatesManager;
    private readonly IRoomsManager _roomsManager;
    private readonly IBot _bot;
    private readonly IBotDbContextFactory _dbContextFactory;

    public DisplayTeamsOnTourHandler(ITemplatesManager templatesManager,
        IRoomsManager roomsManager,
        IBot bot,
        IBotDbContextFactory dbContextFactory)
    {
        _templatesManager = templatesManager;
        _roomsManager = roomsManager;
        _bot = bot;
        _dbContextFactory = dbContextFactory;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "tournament" };

    public override async Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 4 || parts[1] != "tournament" || parts[2] != "create")
        {
            return;
        }

        var format = parts[3].ToLowerAlphaNum();
        if (format.Contains("random"))
        {
            return;
        }

        if (format.StartsWith("gen9"))
        {
            format = format[4..];
        }

        format = format switch
        {
            "nationaldex" => "natdex",
            "almostanyability" => "aaa",
            "anythinggoes" => "ag",
            _ => format
        };

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var teams = await dbContext.Teams.Include(team => team.Rooms)
            .Where(team => team.Rooms != null && team.Rooms.Any(roomTeam => roomTeam.RoomId == roomId) &&
                           team.Format == format)
            .ToListAsync(cancellationToken);
        if (teams.Count == 0)
        {
            return;
        }

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }
        var template = await _templatesManager.GetTemplateAsync("Teams/TeamList", new TeamListViewModel
        {
            Culture = room.Culture,
            Teams = teams,
            TimeZone = room.TimeZone ?? TimeZoneInfo.Local
        });

        _bot.Say(roomId, $"/addhtmlbox {template.RemoveNewlines()}");
    }
}