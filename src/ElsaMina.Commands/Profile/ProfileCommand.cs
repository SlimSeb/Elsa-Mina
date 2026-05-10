using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Profile;

[NamedCommand("profile", Aliases = ["profil"])]
public class ProfileCommand : Command
{
    private readonly IProfileService _profileService;

    public ProfileCommand(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = (context.Target ?? string.Empty).Split(",", 2);
        var userId = string.IsNullOrWhiteSpace(parts[0])
            ? context.Sender.UserId
            : parts[0].ToLowerAlphaNum();

        if (userId == null)
        {
            return;
        }

        var roomId = parts.Length == 2 ? parts[1].ToLowerAlphaNum() : context.RoomId;
        var template = await _profileService.GetProfileHtmlAsync(userId, roomId, cancellationToken);
        if (context.HasRankOrHigher(Rank.Voiced))
        {
            context.SendUpdatableHtml($"profile-{userId}", template.RemoveNewlines(), isChanging: false);
        }
        else
        {
            context.SendPrivateUpdatableHtml(userId, roomId, $"profile-{userId}", template.RemoveNewlines(),
                isChanging: false);
        }
    }
}