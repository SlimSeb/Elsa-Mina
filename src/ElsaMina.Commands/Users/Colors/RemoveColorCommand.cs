using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Users.Colors;

[NamedCommand("removecolor", Aliases = ["removenamecolor", "delnamecolor"])]
public class RemoveColorCommand : Command
{
    private readonly INameColorsService _nameColorsService;

    public RemoveColorCommand(INameColorsService nameColorsService)
    {
        _nameColorsService = nameColorsService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "removecolor_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var userId = context.Target.Trim().ToLowerAlphaNum();
        if (string.IsNullOrEmpty(userId))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var removed = await _nameColorsService.DeleteColorAsync(userId, cancellationToken);
        if (!removed)
        {
            context.ReplyLocalizedMessage("removecolor_not_found", userId);
            return;
        }

        context.ReplyLocalizedMessage("removecolor_success", userId);
    }
}
