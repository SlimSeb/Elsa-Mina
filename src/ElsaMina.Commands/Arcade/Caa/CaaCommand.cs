using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Cloud.Sheets;

namespace ElsaMina.Commands.Arcade.Caa;

[NamedCommand("caa")]
public class CaaCommand : Command
{
    private const int USERNAME_COLUMN = 1;
    private const int POINTS_COLUMN = 2;

    private readonly ISheetProvider _sheetProvider;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public CaaCommand(ISheetProvider sheetProvider, ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _sheetProvider = sheetProvider;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        using var sheet = await _sheetProvider.GetSheetAsync(_configuration.CaaSpreadsheetName,
            _configuration.CaaSheetName, cancellationToken);

        var usernames = await sheet.GetColumnAsync(USERNAME_COLUMN, cancellationToken);
        var pointsColumn = await sheet.GetColumnAsync(POINTS_COLUMN, cancellationToken);

        var players = Enumerable.Zip(usernames.Skip(1), pointsColumn.Skip(1))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.First) && int.TryParse(pair.Second, out _))
            .Select(pair => (UserName: pair.First, Points: int.Parse(pair.Second)))
            .OrderByDescending(pair => pair.Points)
            .Select((pair, index) => new CaaEntry { Rank = index + 1, UserName = pair.UserName, Points = pair.Points })
            .ToArray();

        var template = await _templatesManager.GetTemplateAsync("Arcade/Caa/CaaTable", new CaaViewModel
        {
            Culture = context.Culture,
            Entries = players
        });

        context.ReplyHtml(template.RemoveNewlines().RemoveWhitespacesBetweenTags());
    }
}
