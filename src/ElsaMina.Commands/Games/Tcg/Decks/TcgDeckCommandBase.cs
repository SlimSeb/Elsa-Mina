using ElsaMina.Commands.Games.Tcg.Cards;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Tcg.Decks;

/// <summary>
/// Base for the deck-builder commands. Holds the shared dependencies and the panel-rendering logic
/// so every command can re-render the builder or the deck list after acting. Deck building is
/// personal, so these commands are available in private messages.
/// </summary>
public abstract class TcgDeckCommandBase : Command
{
    protected const string BuilderPageId = "tcg-deck-builder";
    protected const string ListPageId = "tcg-decks";

    protected TcgDeckCommandBase(ITcgDeckService deckService, ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        DeckService = deckService;
        TemplatesManager = templatesManager;
        Configuration = configuration;
    }

    protected ITcgDeckService DeckService { get; }
    protected ITemplatesManager TemplatesManager { get; }
    protected IConfiguration Configuration { get; }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    protected async Task RenderBuilderAsync(IContext context, TcgDeck deck)
    {
        var viewModel = new TcgDeckBuilderViewModel
        {
            Culture = context.Culture,
            BotName = Configuration.Name,
            Trigger = Configuration.Trigger,
            Deck = deck,
            Validation = TcgDeckValidator.Validate(deck),
            AllCards = TcgCardPool.AllCards,
            SelectableEnergyTypes = TcgTypes.SelectableEnergyTypes
        };

        var html = await TemplatesManager.GetTemplateAsync("Games/Tcg/Decks/TcgDeckBuilder", viewModel);
        context.ReplyHtmlPage(BuilderPageId, Clean(html));
    }

    protected async Task RenderListAsync(IContext context, string ownerId,
        CancellationToken cancellationToken)
    {
        var decks = await DeckService.GetUserDecksAsync(ownerId, cancellationToken);
        var summaries = decks
            .Select(deck => new TcgDeckSummary(deck.Name, deck.Cards.Count,
                TcgDeckValidator.Validate(deck).IsValid))
            .ToList();

        var viewModel = new TcgDeckListViewModel
        {
            Culture = context.Culture,
            BotName = Configuration.Name,
            Trigger = Configuration.Trigger,
            Decks = summaries
        };

        var html = await TemplatesManager.GetTemplateAsync("Games/Tcg/Decks/TcgDeckList", viewModel);
        context.ReplyHtmlPage(ListPageId, Clean(html));
    }

    /// <summary>
    /// Reports a deck-operation result, then re-renders the builder when the operation produced an
    /// updated deck (so the panel stays in sync).
    /// </summary>
    protected async Task ApplyResultAsync(IContext context, TcgDeckOperationResult result)
    {
        context.ReplyLocalizedMessage(result.MessageKey, result.Args ?? []);
        if (result is { Success: true, Deck: not null })
        {
            await RenderBuilderAsync(context, result.Deck);
        }
    }

    private static string Clean(string html) =>
        html.RemoveNewlines().CollapseAttributeWhitespace().RemoveWhitespacesBetweenTags();
}
