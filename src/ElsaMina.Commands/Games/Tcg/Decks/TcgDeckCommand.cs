using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tcg.Decks;

[NamedCommand("tcgdeck", Aliases = ["tcgdecks"])]
public class TcgDeckCommand : TcgDeckCommandBase
{
    public TcgDeckCommand(ITcgDeckService deckService, ITemplatesManager templatesManager,
        IConfiguration configuration) : base(deckService, templatesManager, configuration)
    {
    }

    public override string HelpMessageKey => "tcg_deck_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var name = context.Target?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            await RenderListAsync(context, context.Sender.UserId, cancellationToken);
            return;
        }

        var deck = await DeckService.GetDeckAsync(context.Sender.UserId, name, cancellationToken);
        if (deck is null)
        {
            context.ReplyLocalizedMessage("tcg_deck_not_found", name);
            return;
        }

        await RenderBuilderAsync(context, deck);
    }
}
