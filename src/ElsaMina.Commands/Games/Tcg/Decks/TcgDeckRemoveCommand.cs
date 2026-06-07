using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tcg.Decks;

[NamedCommand("tcgdeckremove", Aliases = ["tcgremove", "tcgdeckrem"])]
public class TcgDeckRemoveCommand : TcgDeckCommandBase
{
    public TcgDeckRemoveCommand(ITcgDeckService deckService, ITemplatesManager templatesManager,
        IConfiguration configuration) : base(deckService, templatesManager, configuration)
    {
    }

    public override string HelpMessageKey => "tcg_deckremove_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = (context.Target ?? string.Empty).Split(',', 2);
        if (parts.Length < 2)
        {
            context.ReplyLocalizedMessage("tcg_deckremove_usage", Configuration.Trigger);
            return;
        }

        var deckName = parts[0].Trim();
        var cardId = parts[1].Trim();
        var result = await DeckService.RemoveCardAsync(context.Sender.UserId, deckName, cardId, cancellationToken);
        await ApplyResultAsync(context, result);
    }
}
