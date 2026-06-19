using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Logging;

namespace ElsaMina.Commands.RoomDashboard;

[NamedCommand("room-config", Aliases = ["roomconfig", "rc"])]
public class RoomConfigCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IParametersDefinitionFactory _parametersDefinitionFactory;

    public RoomConfigCommand(IRoomsManager roomsManager, IParametersDefinitionFactory parametersDefinitionFactory)
    {
        _roomsManager = roomsManager;
        _parametersDefinitionFactory = parametersDefinitionFactory;
    }

    public override bool IsWhitelistOnly => true; // todo : only authed used from room
    public override bool IsPrivateMessageOnly => true;

    // TODO à revoir
    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",");
        var roomId = parts[0].Trim().ToLower();

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("room_config_room_not_found", roomId);
            return;
        }

        var roomParameters = _parametersDefinitionFactory.GetParametersDefinitions();
        try
        {
            foreach (var pair in parts.Skip(1))
            {
                var items = pair.Split('=');
                if (items.Length != 2)
                {
                    context.ReplyLocalizedMessage("room_config_invalid_pair", pair);
                    return;
                }

                var parameterId = items[0].Trim();
                var value = items[1].Trim();
                var match = roomParameters
                    .FirstOrDefault(kvp => kvp.Value.Identifier == parameterId);
                if (match.Value == null)
                {
                    context.ReplyLocalizedMessage("room_config_unknown_parameter", parameterId);
                    return;
                }

                var success = await room.SetParameterValueAsync(match.Key, value, cancellationToken);
                if (!success)
                {
                    context.ReplyLocalizedMessage("room_config_invalid_value", value, parameterId);
                    return;
                }
            }

            context.ReplyLocalizedMessage("room_config_success", roomId);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while updating room configuration");
            context.ReplyLocalizedMessage("room_config_failure", exception.Message);
        }
    }
}