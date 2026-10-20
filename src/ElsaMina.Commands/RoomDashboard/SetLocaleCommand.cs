using System.Globalization;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.RoomDashboard;

[NamedCommand("set-locale", Aliases = ["setlocale"])]
public class SetLocaleCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public SetLocaleCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override Rank RequiredRank => Rank.RoomOwner;

    // La room est le 1er argument
    public override string GetTargetRoomId(IContext context) => context.Target.Split(",")[0].Trim();

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var arguments = context.Target.Split(",");
        var roomId = GetTargetRoomId(context);
        var locale = arguments[1].Trim();
        CultureInfo cultureInfo;
        try
        {
            cultureInfo = new CultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            context.ReplyLocalizedMessage("setlocale_invalid_locale", locale);
            return;
        }

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("setlocale_room_not_found", roomId);
            return;
        }
        
        var success = await room.SetParameterValueAsync(Parameter.Locale, locale, cancellationToken);
        context.Culture = cultureInfo;
        if (!success)
        {
            context.ReplyLocalizedMessage("setlocale_failure");
            return;
        }
        context.ReplyLocalizedMessage("setlocale_success", roomId, locale);
    }
}