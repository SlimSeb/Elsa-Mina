using ElsaMina.Commands.Games.Tcg.Cards;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tcg.Decks;

/// <summary>
/// Toggles a single Energy Zone type on a deck. Backing command for the energy selector buttons in
/// the builder panel: clicking a type adds it (up to the max) or removes it (down to the minimum).
/// </summary>
[NamedCommand("tcgdeckenergytoggle", Aliases = ["tcgenergytoggle"])]
public class TcgDeckEnergyToggleCommand : TcgDeckCommandBase
{
    public TcgDeckEnergyToggleCommand(ITcgDeckService deckService, ITemplatesManager templatesManager,
        IConfiguration configuration) : base(deckService, templatesManager, configuration)
    {
    }

    public override bool IsHidden => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = (context.Target ?? string.Empty).Split(',', 2);
        if (parts.Length < 2 || !TcgTypes.TryParse(parts[1], out var type))
        {
            return;
        }

        var deckName = parts[0].Trim();
        var deck = await DeckService.GetDeckAsync(context.Sender.UserId, deckName, cancellationToken);
        if (deck is null)
        {
            context.ReplyLocalizedMessage("tcg_deck_not_found", deckName);
            return;
        }

        var current = deck.EnergyTypes
            .Select(name => TcgTypes.TryParse(name, out var parsed) ? parsed : (TcgType?)null)
            .Where(parsed => parsed.HasValue)
            .Select(parsed => parsed.Value)
            .ToList();

        if (current.Contains(type))
        {
            if (current.Count <= TcgDeckConstants.MIN_ENERGY_TYPES)
            {
                context.ReplyLocalizedMessage("tcg_deck_energy_min", TcgDeckConstants.MIN_ENERGY_TYPES);
                return;
            }

            current.Remove(type);
        }
        else
        {
            if (current.Count >= TcgDeckConstants.MAX_ENERGY_TYPES)
            {
                context.ReplyLocalizedMessage("tcg_deck_energy_max", TcgDeckConstants.MAX_ENERGY_TYPES);
                return;
            }

            current.Add(type);
        }

        var result = await DeckService.SetEnergyTypesAsync(context.Sender.UserId, deckName, current,
            cancellationToken);

        // Re-render even on the "set" result; report only failures to avoid noise on every toggle.
        if (result is { Success: true, Deck: not null })
        {
            await RenderBuilderAsync(context, result.Deck);
        }
        else if (!result.Success)
        {
            context.ReplyLocalizedMessage(result.MessageKey, result.Args ?? []);
        }
    }
}
