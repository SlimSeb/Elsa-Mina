using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Teams.Samples;

[NamedCommand("add-team-to-room", Aliases = ["addteamtoroom", "add-to-room", "add-to-room"])]
public class AddTeamToRoomCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public AddTeamToRoomCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "add_team_to_room_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var teamId = context.Target.ToLowerAlphaNum();
        if (string.IsNullOrEmpty(teamId))
        {
            context.ReplyLocalizedMessage("add_team_to_room_no_arg");
            return;
        }

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var team = await dbContext.Teams
                .Include(team => team.Rooms)
                .FirstOrDefaultAsync(team => team.Id == teamId, cancellationToken);
            if (team == null)
            {
                context.ReplyLocalizedMessage("add_team_to_room_no_team");
                return;
            }

            if (team.Rooms != null && team.Rooms.Any(roomTeam => roomTeam.RoomId == context.RoomId))
            {
                context.ReplyLocalizedMessage("add_team_to_room_team_already_in_room");
                return;
            }

            team.Rooms ??= new List<RoomTeam>();
            team.Rooms.Add(new RoomTeam
            {
                RoomId = context.RoomId,
                TeamId = team.Id
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            context.ReplyLocalizedMessage("add_team_to_room_success");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while adding room to team");
            context.ReplyLocalizedMessage("add_team_to_room_failure", exception.Message);
        }
    }
}