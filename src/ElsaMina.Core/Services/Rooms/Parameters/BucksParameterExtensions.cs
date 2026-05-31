using ElsaMina.Core.Contexts;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.Rooms.Parameters;

public static class BucksParameterExtensions
{
    public static async Task<bool> IsBucksEnabledAsync(this IContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Room == null)
        {
            return false;
        }

        var value = await context.Room.GetParameterValueAsync(Parameter.BucksEnabled, cancellationToken);
        return value.ToBoolean();
    }
}
