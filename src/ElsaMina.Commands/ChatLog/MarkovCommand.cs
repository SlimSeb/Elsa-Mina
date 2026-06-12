using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Utils;
using ElsaMina.FileSharing;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("markov", Aliases = ["imitate", "randomsentence"])]
public class MarkovCommand : BaseMarkovCommand
{
    public MarkovCommand(IFileSharingService fileSharingService, IClockService clockService,
        IConfiguration configuration)
        : base(fileSharingService, clockService, configuration)
    {
    }

    public override string HelpMessageKey => "markov_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var userFilter = string.IsNullOrWhiteSpace(context.Target)
            ? null
            : context.Target.Trim().ToLowerAlphaNum();

        var model = await BuildModelAsync(context, userFilter, cancellationToken);
        if (model == null)
        {
            return;
        }

        var sentence = model.MakeSentence(tries: TRIES, testOutput: false, maxWords: MAX_WORDS);

        if (string.IsNullOrWhiteSpace(sentence))
        {
            context.ReplyLocalizedMessage("markov_not_enough_data");
            return;
        }

        context.Reply(sentence);
    }
}
