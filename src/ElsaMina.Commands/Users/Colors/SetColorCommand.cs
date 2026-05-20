using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Users.Colors;

[NamedCommand("setcolor", Aliases = ["addnamecolor"])]
public class SetColorCommand : Command
{
    private static readonly Regex HEX_COLOR_REGEX = new(
        @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$",
        RegexOptions.Compiled,
        Constants.REGEX_MATCH_TIMEOUT);

    private readonly INameColorsService _nameColorsService;

    public SetColorCommand(INameColorsService nameColorsService)
    {
        _nameColorsService = nameColorsService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "setcolor_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',', 2);
        if (parts.Length != 2)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var userId = parts[0].Trim().ToLowerAlphaNum();
        var color = parts[1].Trim();

        if (!HEX_COLOR_REGEX.IsMatch(color))
        {
            context.ReplyLocalizedMessage("setcolor_invalid_hex");
            return;
        }

        await _nameColorsService.SetColorAsync(userId, color, cancellationToken);
        context.ReplyLocalizedMessage("setcolor_success", color, userId);
    }
}
