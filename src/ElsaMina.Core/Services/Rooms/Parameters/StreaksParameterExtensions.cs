using ElsaMina.Core.Contexts;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.Rooms.Parameters;

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

        var value = await room.GetParameterValueAsync(Parameter.StreaksEnabled, cancellationToken);
        return value.ToBoolean();
    }
}
