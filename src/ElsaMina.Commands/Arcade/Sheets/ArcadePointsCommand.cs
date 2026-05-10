using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Sheets;

namespace ElsaMina.Commands.Arcade.Sheets;

[NamedCommand("arcadepoints", Aliases = ["points"])]
public class ArcadePointsCommand : Command
{
    private const int USERNAME_COLUMN = 1;
    private const int POINTS_COLUMN = 2;

    private readonly ISheetProvider _sheetProvider;
    private readonly IConfiguration _configuration;

    public ArcadePointsCommand(ISheetProvider sheetProvider, IConfiguration configuration)
    {
        _sheetProvider = sheetProvider;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "arcade_sheets_points_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var target = context.Target.Trim();
        if (string.IsNullOrEmpty(target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var normalizedTarget = target.ToLowerAlphaNum();

        using var sheet = await _sheetProvider.GetSheetAsync(_configuration.ArcadeSpreadsheetName,
            _configuration.ArcadeHallOfFameSheetName, cancellationToken);

        var usernames = await sheet.GetColumnAsync(USERNAME_COLUMN, cancellationToken);
        var points = await sheet.GetColumnAsync(POINTS_COLUMN, cancellationToken);

        var userPoints = 0;
        foreach (var (name, pointStr) in usernames.Skip(1).Zip(points.Skip(1)))
        {
            if (name.ToLowerAlphaNum() == normalizedTarget
                && int.TryParse(pointStr, out var parsed))
            {
                userPoints = parsed;
                break;
            }
        }

        if (userPoints > 0)
        {
            context.ReplyLocalizedMessage("arcade_sheets_points_has_points", target, userPoints);
        }
        else
        {
            context.ReplyLocalizedMessage("arcade_sheets_points_no_points", target);
        }
    }
}