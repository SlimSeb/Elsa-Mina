using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Sheets;

namespace ElsaMina.Commands.Arcade.Sheets;

[NamedCommand("arcadehof", "arcade-hof", "arcade-hall-of-fame")]
public class ArcadeHallOfFameCommand : Command
{
    private readonly ISheetProvider _sheetProvider;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public ArcadeHallOfFameCommand(ISheetProvider sheetProvider, ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _sheetProvider = sheetProvider;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;

    private const int PAGE_SIZE = 50;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var page = int.TryParse(context.Target.Trim(), out var parsedPage)
            ? Math.Max(1, parsedPage)
            : 1;

        using var sheet = await _sheetProvider.GetSheetAsync(_configuration.ArcadeSpreadsheetName,
            _configuration.ArcadeHallOfFameSheetName, cancellationToken);
        var allEntries = await GetHallOfFameEntriesAsync(sheet, cancellationToken);

        var totalPages = (int)Math.Ceiling(allEntries.Length / (double)PAGE_SIZE);
        totalPages = Math.Max(1, totalPages);
        page = Math.Clamp(page, 1, totalPages);

        var entries = allEntries.Skip((page - 1) * PAGE_SIZE).Take(PAGE_SIZE).ToArray();

        var viewModel = new ArcadeHallOfFameViewModel
        {
            Culture = context.Culture,
            Entries = entries,
            Page = page,
            TotalPages = totalPages,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger
        };

        var template = await _templatesManager.GetTemplateAsync("Arcade/Sheets/ArcadeHallOfFame", viewModel);

        context.ReplyHtmlPage("arcade-hof",
            template.RemoveNewlines().CollapseAttributeWhitespace().RemoveWhitespacesBetweenTags());
    }

    private static async Task<ArcadeHallOfFameEntry[]> GetHallOfFameEntriesAsync(ISheet sheet,
        CancellationToken cancellationToken)
    {
        var ranks = await sheet.GetColumnAsync(0, cancellationToken);
        var usernames = await sheet.GetColumnAsync(1, cancellationToken);
        var points = await sheet.GetColumnAsync(2, cancellationToken);

        return Enumerable.Zip(ranks.Skip(1), usernames.Skip(1), points.Skip(1))
            .Where(tuple => IsValidEntry(tuple.First, tuple.Second, tuple.Third))
            .Select(tuple => CreateEntry(tuple.First, tuple.Second, tuple.Third))
            .Where(tuple => tuple.Points > 0)
            .ToArray();
    }

    private static bool IsValidEntry(string rank, string username, string point) =>
        !string.IsNullOrWhiteSpace(rank) && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(point);

    private static ArcadeHallOfFameEntry CreateEntry(string rank, string username, string point) =>
        new()
        {
            Rank = int.Parse(rank),
            UserName = username,
            Points = int.Parse(point)
        };
}