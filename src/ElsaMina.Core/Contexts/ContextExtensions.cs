using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Contexts;

public static class ContextExtensions
{
    /// <summary>
    /// L'argument n° <paramref name="index"/> de la commande lu comme un id de room. Null s'il manque ou s'il est vide.
    /// A utiliser dans <see cref="Services.Commands.ICommand.GetTargetRoomId"/> et dans RunAsync, comme ça le rang est
    /// vérifié dans la room où la commande agit
    /// </summary>
    public static string GetRoomIdArgument(this IContext context, int index, string separator = ",")
    {
        var arguments = context.Target?.Split(separator) ?? [];
        if (index >= arguments.Length)
        {
            return null;
        }

        var roomId = arguments[index].ToLowerAlphaNum();
        return string.IsNullOrEmpty(roomId) ? null : roomId;
    }
}
