using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tcg.Decks;

[NamedCommand("tcgdecknew", Aliases = ["tcgnewdeck", "tcgdeckcreate"])]
public class TcgDeckNewCommand : TcgDeckCommandBase
{
    public TcgDeckNewCommand(ITcgDeckService deckService, ITemplatesManager templatesManager,
        IConfiguration configuration) : base(deckService, templatesManager, configuration)
    {
    }

    public override string HelpMessageKey => "tcg_decknew_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var result = await DeckService.CreateAsync(context.Sender.UserId, context.Target, cancellationToken);
        await ApplyResultAsync(context, result);
    }
}
