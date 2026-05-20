using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Showdown;

[NamedCommand("showavy")]
public class ShowAvatarCommand : Command
{
    private const string AVATAR_URL_TEMPLATE = "https://play.pokemonshowdown.com/sprites/trainers/{0}.png";

    private readonly IHttpService _httpService;

    public ShowAvatarCommand(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "showavy_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var avatarName = context.Target.Trim();
        if (string.IsNullOrEmpty(avatarName))
        {
            context.ReplyLocalizedMessage("showavy_no_target");
            return;
        }

        var avatarUrl = string.Format(AVATAR_URL_TEMPLATE, avatarName);
        try
        {
            await _httpService.GetAsync<string>(avatarUrl, isRaw: true, cancellationToken: cancellationToken);
            context.Reply($"!show {avatarUrl}", rankAware: true);
        }
        catch (HttpException)
        {
            context.ReplyLocalizedMessage("showavy_not_found", avatarName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while fetching avatar {AvatarName}", avatarName);
            context.ReplyLocalizedMessage("showavy_not_found", avatarName);
        }
    }
}
