using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Permissions;

public class PermissionTargetResolver : IPermissionTargetResolver
{
    private const string CATEGORY_PREFIX = "category:";

    private static readonly Dictionary<string, Rank> RANK_NAMES = new(StringComparer.OrdinalIgnoreCase)
    {
        ["voice"] = Rank.Voiced,
        ["voiced"] = Rank.Voiced,
        ["driver"] = Rank.Driver,
        ["mod"] = Rank.Mod,
        ["moderator"] = Rank.Mod
    };

    private readonly ICommandRegistry _commandRegistry;

    public PermissionTargetResolver(ICommandRegistry commandRegistry)
    {
        _commandRegistry = commandRegistry;
    }

    public PermissionTarget Resolve(string text)
    {
        var trimmedText = text?.Trim();
        if (string.IsNullOrEmpty(trimmedText))
        {
            return null;
        }

        if (TryResolveRank(trimmedText, out var rank))
        {
            return new PermissionTarget(PermissionGrantKind.Rank, User.GetCharacterFromRank(rank).ToString());
        }

        if (trimmedText.StartsWith(CATEGORY_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveCategory(trimmedText[CATEGORY_PREFIX.Length..].Trim());
        }

        var command = _commandRegistry.Find(trimmedText.ToLowerInvariant());
        return command != null
            ? new PermissionTarget(PermissionGrantKind.Command, command.Name)
            : ResolveCategory(trimmedText);
    }

    private static bool TryResolveRank(string text, out Rank rank)
    {
        if (RANK_NAMES.TryGetValue(text, out rank))
        {
            return true;
        }

        rank = text.Length == 1 ? User.GetRankFromCharacter(text[0]) : Rank.Regular;
        return rank != Rank.Regular;
    }

    private PermissionTarget ResolveCategory(string text)
    {
        var category = _commandRegistry.Commands
            .Select(command => command.Category)
            .FirstOrDefault(category => !string.IsNullOrEmpty(category)
                                        && string.Equals(category, text, StringComparison.OrdinalIgnoreCase));
        return category == null ? null : new PermissionTarget(PermissionGrantKind.Category, category);
    }
}
