using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.FileSharing;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("linecount")]
public class LinecountCommand : Command
{
    private readonly IFileSharingService _fileSharingService;
    private readonly ITemplatesManager _templatesManager;

    public LinecountCommand(IFileSharingService fileSharingService, ITemplatesManager templatesManager)
    {
        _fileSharingService = fileSharingService;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "linecount_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',');
        if (parts.Length < 2)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var userId = parts[0].Trim().ToLowerAlphaNum();
        var roomId = parts[1].Trim().ToLowerAlphaNum();

        var now = DateTime.UtcNow;
        var keys = await _fileSharingService.ListFilesAsync(
            ChatLogHelpers.GetS3MonthPrefix(roomId, now.Year, now.Month), cancellationToken);

        if (keys.Count == 0)
        {
            context.ReplyLocalizedMessage("linecount_no_logs");
            return;
        }

        var counts = new SortedDictionary<int, int>();
        var totalCount = 0;
        var maxCount = 0;

        foreach (var key in keys)
        {
            var day = ParseDayFromKey(key);
            if (day == 0) continue;

            var dayCount = 0;
            await using var stream = await _fileSharingService.GetFileAsync(key, cancellationToken);
            if (stream == null) continue;

            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (ChatLogHelpers.TryParseLine(line, out var username, out _)
                    && username.ToLowerAlphaNum() == userId)
                {
                    dayCount++;
                }
            }

            counts[day] = dayCount;
            totalCount += dayCount;
            if (dayCount > maxCount) maxCount = dayCount;
        }

        var days = counts.Select(kv => new LinecountDay { Day = kv.Key, Count = kv.Value }).ToList();
        var avgPerDay = days.Count > 0 ? (double)totalCount / days.Count : 0;

        var html = await _templatesManager.GetTemplateAsync("ChatLog/Linecount",
            new LinecountViewModel
            {
                Culture = context.Culture,
                Days = days,
                Month = now.Month,
                MaxCount = maxCount,
                TotalCount = totalCount,
                AvgPerDay = avgPerDay
            });
        context.ReplyHtml(html.RemoveNewlines().CollapseAttributeWhitespace());
    }

    private static int ParseDayFromKey(string key)
    {
        var fileName = Path.GetFileNameWithoutExtension(key); // "yyyy-MM-dd"
        return fileName.Length == 10 && int.TryParse(fileName[8..], out var day) ? day : 0;
    }
}
