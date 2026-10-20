using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Authorization;

namespace ElsaMina.Commands.Permissions;

public static class PermissionsFormatter
{
    public static string FormatTarget(IContext context, PermissionGrantKind kind, string target) => kind switch
    {
        PermissionGrantKind.Rank => context.GetString("permissions_target_rank", target),
        PermissionGrantKind.Command => context.GetString("permissions_target_command", target),
        _ => context.GetString("permissions_target_category", target)
    };

    /// <summary>
    /// La date d'expiration dans le fuseau de la room, ou "jamais" pour un droit permanent
    /// </summary>
    public static string FormatExpiry(IContext context, DateTimeOffset? expiresAt, TimeZoneInfo timeZone) =>
        expiresAt == null
            ? context.GetString("permissions_never_expires")
            : TimeZoneInfo.ConvertTime(expiresAt.Value, timeZone).ToString("g", context.Culture);
}
