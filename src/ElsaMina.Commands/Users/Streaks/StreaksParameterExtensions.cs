using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Users.Streaks;

public static class StreaksParameterExtensions
{
    public static async Task<bool> IsStreaksEnabledAsync(this IContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Room == null)
        {
            return true;
        }

        return await context.Room.IsStreaksEnabledAsync(cancellationToken);
    }

    public static async Task<bool> IsStreaksEnabledAsync(this IRoom room,
        CancellationToken cancellationToken = default)
    {
        if (room == null)
        {
            return true;
        }

        var value = await room.GetParameterValueAsync(StreaksRoomParameters.StreaksEnabled, cancellationToken);
        return value.ToBoolean();
    }
}
