using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.FileSharing;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("markov", Aliases = ["imitate", "randomsentence"])]
public class MarkovCommand : Command
{
    private const string StartToken = "";
    private const string EndToken = "";
    private const int MinMessages = 20;
    private const int MaxWords = 40;

    private readonly IFileSharingService _fileSharingService;
    private readonly IClockService _clockService;
    private readonly IConfiguration _configuration;
    private readonly IRandomService _randomService;

    public MarkovCommand(IFileSharingService fileSharingService, IClockService clockService,
        IConfiguration configuration, IRandomService randomService)
    {
        _fileSharingService = fileSharingService;
        _clockService = clockService;
        _configuration = configuration;
        _randomService = randomService;
    }

    public override Rank RequiredRank => Rank.Admin;
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

        if (messages.Count < MinMessages)
        {
            context.ReplyLocalizedMessage("markov_not_enough_data");
            return;
        }

        var sentence = GenerateSentence(messages);
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

    private string GenerateSentence(IReadOnlyList<string> messages)
    {
        var transitions = new Dictionary<string, List<string>>();
        foreach (var message in messages)
        {
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length == 0)
            {
                continue;
            }

            var tokens = new List<string> { StartToken };
            tokens.AddRange(words);
            tokens.Add(EndToken);

            for (var i = 0; i < tokens.Count - 1; i++)
            {
                var key = tokens[i];
                if (!transitions.TryGetValue(key, out var nextWords))
                {
                    nextWords = [];
                    transitions[key] = nextWords;
                }

                nextWords.Add(tokens[i + 1]);
            }
        }

        var current = StartToken;
        var result = new List<string>();
        while (result.Count < MaxWords && transitions.TryGetValue(current, out var candidates))
        {
            var next = _randomService.RandomElement(candidates);
            if (next == EndToken)
            {
                break;
            }

            result.Add(next);
            current = next;
        }

        return string.Join(' ', result);
    }
}
