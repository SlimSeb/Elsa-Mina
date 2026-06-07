using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tcg.Decks;

[NamedCommand("tcgdeckadd", Aliases = ["tcgadd"])]
public class TcgDeckAddCommand : TcgDeckCommandBase
{
    public TcgDeckAddCommand(ITcgDeckService deckService, ITemplatesManager templatesManager,
        IConfiguration configuration) : base(deckService, templatesManager, configuration)
    {
    }

    public override string HelpMessageKey => "tcg_deckadd_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = (context.Target ?? string.Empty).Split(',', 2);
        if (parts.Length < 2)
        {
            context.ReplyLocalizedMessage("tcg_deckadd_usage", Configuration.Trigger);
            return;
        }

        var deckName = parts[0].Trim();
        var cardId = parts[1].Trim();
        var result = await DeckService.AddCardAsync(context.Sender.UserId, deckName, cardId, cancellationToken);
        await ApplyResultAsync(context, result);
    }
}
