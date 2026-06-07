using ElsaMina.Commands.Games.Tcg.Decks;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Tcg.Cards;

/// <summary>
/// Shows the full card pool as a read-only reference panel. When given the name of a deck the sender
/// owns, the panel also shows "+" buttons that add a card to that deck.
/// </summary>
[NamedCommand("tcgcards", Aliases = ["tcgcardpool", "tcgpool"])]
public class TcgCardsCommand : Command
{
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly ITcgDeckService _deckService;

    public TcgCardsCommand(ITemplatesManager templatesManager, IConfiguration configuration,
        ITcgDeckService deckService)
    {
        _templatesManager = templatesManager;
        _configuration = configuration;
        _deckService = deckService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "tcg_cards_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string targetDeckName = null;
        var requestedDeck = context.Target?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedDeck))
        {
            var deck = await _deckService.GetDeckAsync(context.Sender.UserId, requestedDeck, cancellationToken);
            if (deck is null)
            {
                context.ReplyLocalizedMessage("tcg_deck_not_found", requestedDeck);
                return;
            }

            targetDeckName = deck.Name;
        }

        var viewModel = new TcgCardBrowserViewModel
        {
            Culture = context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            Cards = TcgCardPool.AllCards,
            TargetDeckName = targetDeckName
        };

        var html = await _templatesManager.GetTemplateAsync("Games/Tcg/Cards/TcgCardBrowser", viewModel);
        context.ReplyHtmlPage("tcg-cards", html.RemoveNewlines().CollapseAttributeWhitespace()
            .RemoveWhitespacesBetweenTags());
    }
}
