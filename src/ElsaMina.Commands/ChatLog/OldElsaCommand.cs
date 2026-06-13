using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("oldelsa", Aliases = ["oldmarkov", "legacymarkov"])]
public class OldElsaCommand : Command
{
    private readonly IOldElsaModelService _oldElsaModelService;

    public OldElsaCommand(IOldElsaModelService oldElsaModelService)
    {
        _oldElsaModelService = oldElsaModelService;
    }

    public override Rank RequiredRank => Rank.Admin;
    public override string HelpMessageKey => "old_elsa_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var sentence = _oldElsaModelService.GenerateSentence();

        if (string.IsNullOrWhiteSpace(sentence))
        {
            context.ReplyLocalizedMessage("old_elsa_not_found");
            return Task.CompletedTask;
        }

        context.Reply(sentence);
        return Task.CompletedTask;
    }
}
