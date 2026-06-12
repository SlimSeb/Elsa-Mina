using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.FileSharing;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("markovstart", Aliases = ["startsentence", "continuesentence"])]
public class MarkovStartCommand : BaseMarkovCommand
{
    public MarkovStartCommand(IFileSharingService fileSharingService, IClockService clockService,
        IConfiguration configuration)
        : base(fileSharingService, clockService, configuration)
    {
    }

    public override string HelpMessageKey => "markov_start_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            context.ReplyLocalizedMessage("markov_start_no_words");
            return;
        }

        var words = context.Target.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var model = await BuildModelAsync(context, userFilter: null, cancellationToken);
        if (model == null)
        {
            return;
        }

        // MakeSentenceWithStart only seeds from up to STATE_SIZE words, so when the user provides
        // more, we keep the extra leading words as a literal prefix and seed from the last ones.
        var seedCount = Math.Min(words.Length, STATE_SIZE);
        var leadingWords = words[..^seedCount];
        var seed = string.Join(' ', words[^seedCount..]);

        var continuation = model.MakeSentenceWithStart(seed, strict: false, tries: TRIES, testOutput: false);

        if (string.IsNullOrWhiteSpace(continuation))
        {
            context.ReplyLocalizedMessage("markov_start_not_found");
            return;
        }

        var sentence = leadingWords.Length == 0
            ? continuation
            : $"{string.Join(' ', leadingWords)} {continuation}";

        context.Reply(sentence);
    }
}
