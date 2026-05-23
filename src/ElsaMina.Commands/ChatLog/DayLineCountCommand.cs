using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.FileSharing;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("daylinecount", Aliases = ["daylc"])]
public class DayLineCountCommand : Command
{
    private readonly IFileSharingService _fileSharingService;
    private readonly ITemplatesManager _templatesManager;

    public DayLineCountCommand(IFileSharingService fileSharingService, ITemplatesManager templatesManager)
    {
        _fileSharingService = fileSharingService;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsWhitelistOnly => true;
    public override string HelpMessageKey => "daylinecount_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',');
        var roomId = parts.Length >= 2 ? parts[1].Trim().ToLowerAlphaNum() : context.RoomId;

        if (!DateOnly.TryParse(parts[0].Trim(), context.Culture, out var date))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        await using var stream = await _fileSharingService.GetFileAsync(
            ChatLogHelpers.GetS3Key(roomId, date), cancellationToken);

        if (stream == null)
        {
            context.ReplyLocalizedMessage("daylinecount_no_logs");
            return;
        }

        var counts = new Dictionary<string, (int Messages, int Words, int Chars)>();
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!ChatLogHelpers.TryParseLine(line, out var username, out var message))
            {
                continue;
            }

            var userId = username.ToLowerAlphaNum();
            if (string.IsNullOrEmpty(userId))
            {
                userId = "[server]";
            }
            var wordCount = message.Split(' ').Length;
            if (counts.TryGetValue(userId, out var existing))
            {
                counts[userId] = (existing.Messages + 1, existing.Words + wordCount, existing.Chars + message.Length);
            }
            else
            {
                counts[userId] = (1, wordCount, message.Length);
            }
        }

        if (counts.Count == 0)
        {
            context.ReplyLocalizedMessage("daylinecount_no_messages");
            return;
        }

        var rows = counts
            .OrderByDescending(kv => kv.Value.Messages)
            .Select(kv => new DayLineCountRow
            {
                UserId = kv.Key,
                Color = kv.Key.ToColorHexCodeWithCustoms(),
                Messages = kv.Value.Messages,
                Words = kv.Value.Words,
                Chars = kv.Value.Chars
            })
            .ToList();

        var html = await _templatesManager.GetTemplateAsync("ChatLog/DayLineCount",
            new DayLineCountViewModel { Culture = context.Culture, Rows = rows });
        context.ReplyHtml(html.RemoveNewlines().CollapseAttributeWhitespace());
    }
}
