using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.FileSharing;
using Lusamine.Markovify;

namespace ElsaMina.Commands.ChatLog;

public abstract class BaseMarkovCommand : Command
{
    protected const int MIN_MESSAGES = 20;
    protected const int STATE_SIZE = 2;
    protected const int MAX_WORDS = 40;
    protected const int TRIES = 50;
    protected const double TEMPERATURE = 1.75;

    private readonly IFileSharingService _fileSharingService;
    private readonly IClockService _clockService;
    private readonly IConfiguration _configuration;

    protected BaseMarkovCommand(IFileSharingService fileSharingService, IClockService clockService,
        IConfiguration configuration)
    {
        _fileSharingService = fileSharingService;
        _clockService = clockService;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Voiced;

    /// <summary>
    /// Loads this month's chat logs and trains a Markov model from them. When there are no logs
    /// or not enough usable messages, this replies with the appropriate localized message and
    /// returns <c>null</c>.
    /// </summary>
    protected async Task<NewlineText> BuildModelAsync(IContext context, string userFilter,
        CancellationToken cancellationToken)
    {
        var now = _clockService.CurrentUtcDateTime;
        var keys = await _fileSharingService.ListFilesAsync(
            ChatLogHelpers.GetS3MonthPrefix(context.RoomId, now.Year, now.Month), cancellationToken);

        if (keys.Count == 0)
        {
            context.ReplyLocalizedMessage("markov_no_logs");
            return null;
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
            return null;
        }

        // Each chat message is treated as its own sentence, so NewlineText (which splits
        // on line boundaries rather than punctuation) is the right model for chat logs
        var corpus = string.Join('\n', messages);
        return new NewlineText(corpus, stateSize: STATE_SIZE, normalize: false, temperature: TEMPERATURE);
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
