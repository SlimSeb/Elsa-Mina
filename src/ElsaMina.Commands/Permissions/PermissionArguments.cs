using ElsaMina.Core.Contexts;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Permissions;

/// <summary>
/// Les arguments des commandes de droits. En PM la room est le dernier argument, en room c'est celle du message
/// </summary>
public sealed record PermissionArguments(string RoomId, IReadOnlyList<string> Values)
{
    public static PermissionArguments Parse(IContext context)
    {
        var parts = string.IsNullOrWhiteSpace(context.Target)
            ? []
            : context.Target.Split(',').Select(part => part.Trim()).ToArray();
        if (!context.IsPrivateMessage)
        {
            return new PermissionArguments(context.RoomId, parts);
        }

        if (parts.Length == 0)
        {
            return new PermissionArguments(null, parts);
        }

        var roomId = parts[^1].ToLowerAlphaNum();
        return new PermissionArguments(string.IsNullOrEmpty(roomId) ? null : roomId, parts[..^1]);
    }
}
