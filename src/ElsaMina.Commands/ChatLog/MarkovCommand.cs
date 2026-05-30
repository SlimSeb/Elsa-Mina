using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.FileSharing;
using Lusamine.Markovify;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("markov", Aliases = ["imitate", "randomsentence"])]
public class MarkovCommand : Command
{
    private const int MIN_MESSAGES = 20;
    private const int STATE_SIZE = 2;
    private const int MAX_WORDS = 40;
    private const int TRIES = 50;

    private readonly IFileSharingService _fileSharingService;
    private readonly IClockService _clockService;
    private readonly IConfiguration _configuration;

    public MarkovCommand(IFileSharingService fileSharingService, IClockService clockService,
        IConfiguration configuration)
    {
        _fileSharingService = fileSharingService;
        _clockService = clockService;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "markov_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var userFilter = string.IsNullOrWhiteSpace(context.Target)
            ? null
            : context.Target.Trim().ToLowerAlphaNum();

        var now = _clockService.CurrentUtcDateTime;
        var keys = await _fileSharingService.ListFilesAsync(
            ChatLogHelpers.GetS3MonthPrefix(context.RoomId, now.Year, now.Month), cancellationToken);

        if (keys.Count == 0)
        {
            context.ReplyLocalizedMessage("markov_no_logs");
            return;
        }

        var messages = new List<string>();
        foreach (var key in keys)
        {
            await using var stream = await _fileSharingService.GetFileAsync(key, cancellationToken);
            if (stream == null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!ChatLogHelpers.TryParseLine(line, out var username, out var message))
                {
                    continue;
                }

                if (userFilter != null && username.ToLowerAlphaNum() != userFilter)
                {
                    continue;
                }

                if (IsUsableMessage(message))
                {
                    messages.Add(message.Trim());
                }
            }
        }

        if (messages.Count < MIN_MESSAGES)
        {
            context.ReplyLocalizedMessage("markov_not_enough_data");
            return;
        }

        // Each chat message is treated as its own sentence, so NewlineText (which splits
        // on line boundaries rather than punctuation) is the right model for chat logs
        var corpus = string.Join('\n', messages);
        var model = new NewlineText(corpus, stateSize: STATE_SIZE);
        var sentence = model.MakeSentence(tries: TRIES, testOutput: false, maxWords: MAX_WORDS);

        if (string.IsNullOrWhiteSpace(sentence))
        {
            context.ReplyLocalizedMessage("markov_not_enough_data");
            return;
        }

        context.Reply(sentence);
    }

    private bool IsUsableMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var trimmed = message.TrimStart();
        return !trimmed.StartsWith(_configuration.Trigger, StringComparison.Ordinal)
               && !trimmed.StartsWith('/')
               && !trimmed.StartsWith('!');
    }
}
