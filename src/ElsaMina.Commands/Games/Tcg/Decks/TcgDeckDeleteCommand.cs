using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tcg.Decks;

[NamedCommand("tcgdeckdelete", Aliases = ["tcgdeckdel", "tcgdeletedeck"])]
public class TcgDeckDeleteCommand : TcgDeckCommandBase
{
    public TcgDeckDeleteCommand(ITcgDeckService deckService, ITemplatesManager templatesManager,
        IConfiguration configuration) : base(deckService, templatesManager, configuration)
    {
    }

    public override string HelpMessageKey => "tcg_deckdelete_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var name = context.Target?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            context.ReplyLocalizedMessage("tcg_deckdelete_usage", Configuration.Trigger);
            return;
        }

        var result = await DeckService.DeleteAsync(context.Sender.UserId, name, cancellationToken);
        context.ReplyLocalizedMessage(result.MessageKey, result.Args ?? []);
        if (result.Success)
        {
            await RenderListAsync(context, context.Sender.UserId, cancellationToken);
        }
    }
}
