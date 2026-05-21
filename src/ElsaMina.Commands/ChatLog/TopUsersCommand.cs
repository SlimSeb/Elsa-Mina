using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.FileSharing;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("topusers")]
public class TopUsersCommand : Command
{
    private readonly IFileSharingService _fileSharingService;
    private readonly ITemplatesManager _templatesManager;

    public TopUsersCommand(IFileSharingService fileSharingService, ITemplatesManager templatesManager)
    {
        _fileSharingService = fileSharingService;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = string.IsNullOrWhiteSpace(context.Target)
            ? context.RoomId
            : context.Target.Trim().ToLowerAlphaNum();

        if (!await context.HasSufficientRankInRoom(roomId, RequiredRank, cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var keys = await _fileSharingService.ListFilesAsync(
            ChatLogHelpers.GetS3MonthPrefix(roomId, now.Year, now.Month), cancellationToken);

        if (keys.Count == 0)
        {
            context.ReplyLocalizedMessage("topusers_no_logs");
            return;
        }

        var counts = new Dictionary<string, int>();
        foreach (var key in keys)
        {
            await using var stream = await _fileSharingService.GetFileAsync(key, cancellationToken);
            if (stream == null) continue;

            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!ChatLogHelpers.TryParseLine(line, out var username, out _)) continue;
                var uid = username.ToLowerAlphaNum();
                counts[uid] = counts.GetValueOrDefault(uid) + 1;
            }
        }

        var sorted = counts.OrderByDescending(kv => kv.Value).Take(20).ToList();
        var maxCount = sorted.Count > 0 ? sorted[0].Value : 1;

        var users = sorted.Select(kv => new TopUsersRow { UserId = kv.Key, Count = kv.Value }).ToList();

        var html = await _templatesManager.GetTemplateAsync("ChatLog/TopUsers",
            new TopUsersViewModel
            {
                Culture = context.Culture,
                RoomId = roomId,
                Users = users,
                MaxCount = maxCount
            });
        context.ReplyHtml(html.RemoveNewlines().CollapseAttributeWhitespace());
    }
}
