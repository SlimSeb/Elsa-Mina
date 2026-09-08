using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Cloud.Sheets;

namespace ElsaMina.Commands.Arcade.Sheets;

[NamedCommand("addpoints", Aliases = ["hof-addpoints", "hofaddpoints"])]
public class ArcadeSheetAddPointsCommand : Command
{
    private const int USERNAME_COLUMN = 1;
    private const int USER_ID_COLUMN = 8;
    private const int POINTS_COLUMN = 9;

    private readonly ISheetProvider _sheetProvider;
    private readonly IConfiguration _configuration;

    public ArcadeSheetAddPointsCommand(ISheetProvider sheetProvider, IConfiguration configuration)
    {
        _sheetProvider = sheetProvider;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "arcade_sheets_addpoints_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[1].Trim(), out var points))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var userId = parts[0].Trim().ToLowerAlphaNum();

        using var sheet = await _sheetProvider.GetSheetAsync(_configuration.ArcadeSpreadsheetName,
            _configuration.ArcadeHallOfFameSheetName, cancellationToken);

        var userIds = await sheet.GetColumnAsync(USER_ID_COLUMN, cancellationToken);
        var foundRow = FindUserRow(userIds, userId);

        if (foundRow.HasValue)
        {
            var currentPointsStr = await sheet.GetCellAsync(POINTS_COLUMN, foundRow.Value, cancellationToken);
            var currentPoints = int.TryParse(currentPointsStr, out var parsed) ? parsed : 0;
            await sheet.SetCellAsync(POINTS_COLUMN, foundRow.Value, (currentPoints + points).ToString(),
                cancellationToken);
            await sheet.FlushAsync(cancellationToken);
            context.ReplyLocalizedMessage("arcade_sheets_addpoints_success");
        }
        else
        {
            var usernames = await sheet.GetColumnAsync(USERNAME_COLUMN, cancellationToken);
            var nextRow = GetNextRow(usernames);
            await sheet.SetCellAsync(USER_ID_COLUMN, nextRow, userId, cancellationToken);
            await sheet.SetCellAsync(POINTS_COLUMN, nextRow, points.ToString(), cancellationToken);
            await sheet.FlushAsync(cancellationToken);
            context.ReplyLocalizedMessage("arcade_sheets_addpoints_new_player");
        }
    }

    private static int? FindUserRow(IReadOnlyList<string> userIds, string userId)
    {
        for (var row = 0; row < userIds.Count; row++)
        {
            if (userIds[row] == userId)
            {
                return row;
            }
        }

        return null;
    }

    private static int GetNextRow(IReadOnlyList<string> column)
    {
        for (var row = column.Count - 1; row >= 0; row--)
        {
            if (!string.IsNullOrWhiteSpace(column[row]))
            {
                return row + 1;
            }
        }

        return 0;
    }
}