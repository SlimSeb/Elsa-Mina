using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("oldelsastart", Aliases = ["oldmarkovstart", "legacymarkovstart"])]
public class OldElsaStartCommand : Command
{
    private readonly IOldElsaModelService _oldElsaModelService;

    public OldElsaStartCommand(IOldElsaModelService oldElsaModelService)
    {
        _oldElsaModelService = oldElsaModelService;
    }

    public override Rank RequiredRank => Rank.Admin;
    public override string HelpMessageKey => "old_elsa_start_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            context.ReplyLocalizedMessage("old_elsa_start_no_words");
            return Task.CompletedTask;
        }

        var sentence = _oldElsaModelService.GenerateSentence(context.Target.Trim());

        if (string.IsNullOrWhiteSpace(sentence))
        {
            context.ReplyLocalizedMessage("old_elsa_start_not_found");
            return Task.CompletedTask;
        }

        context.Reply(sentence);
        return Task.CompletedTask;
    }
}
