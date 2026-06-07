using ElsaMina.Commands.Games.Tcg.Cards;
using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Tcg.Decks;

public class TcgDeckService : ITcgDeckService
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IClockService _clockService;

    public TcgDeckService(IBotDbContextFactory dbContextFactory, IClockService clockService)
    {
        _dbContextFactory = dbContextFactory;
        _clockService = clockService;
    }

    public async Task<IReadOnlyList<TcgDeck>> GetUserDecksAsync(string ownerId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.TcgDecks
            .Where(deck => deck.OwnerId == ownerId)
            .OrderBy(deck => deck.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<TcgDeck> GetDeckAsync(string ownerId, string name,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await FindDeckAsync(dbContext, ownerId, name, cancellationToken);
    }

    public async Task<TcgDeckOperationResult> CreateAsync(string ownerId, string name,
        CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return TcgDeckOperationResult.Fail("tcg_deck_name_required");
        }

        if (name.Length > TcgDeckConstants.MAX_NAME_LENGTH)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_name_too_long", TcgDeckConstants.MAX_NAME_LENGTH);
        }

        // Deck names are referenced inside comma-delimited command arguments, so commas are not allowed.
        if (name.Contains(','))
        {
            return TcgDeckOperationResult.Fail("tcg_deck_name_no_comma");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await FindDeckAsync(dbContext, ownerId, name, cancellationToken);
        if (existing is not null)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_already_exists", name);
        }

        var deckCount = await dbContext.TcgDecks.CountAsync(deck => deck.OwnerId == ownerId, cancellationToken);
        if (deckCount >= TcgDeckConstants.MAX_DECKS_PER_USER)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_limit_reached", TcgDeckConstants.MAX_DECKS_PER_USER);
        }

        await dbContext.EnsureUserExistsAsync(ownerId, cancellationToken);
        var deck = new TcgDeck
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = ownerId,
            Name = name,
            CreationDate = _clockService.CurrentUtcDateTime
        };
        dbContext.TcgDecks.Add(deck);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TcgDeckOperationResult.Ok("tcg_deck_created", deck, name);
    }

    public async Task<TcgDeckOperationResult> AddCardAsync(string ownerId, string name, string cardId,
        CancellationToken cancellationToken = default)
    {
        if (!TcgCardPool.TryGet(cardId, out var card))
        {
            return TcgDeckOperationResult.Fail("tcg_deck_unknown_card", cardId);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var deck = await FindDeckAsync(dbContext, ownerId, name, cancellationToken);
        if (deck is null)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_not_found", name);
        }

        var cards = deck.Cards;
        if (cards.Count >= TcgDeckConstants.DECK_SIZE)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_full", TcgDeckConstants.DECK_SIZE);
        }

        if (cards.Count(id => id == card.Id) >= TcgDeckConstants.MAX_COPIES)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_max_copies", card.Name, TcgDeckConstants.MAX_COPIES);
        }

        cards.Add(card.Id);
        deck.Cards = cards;
        await dbContext.SaveChangesAsync(cancellationToken);

        return TcgDeckOperationResult.Ok("tcg_deck_card_added", deck, card.Name, cards.Count,
            TcgDeckConstants.DECK_SIZE);
    }

    public async Task<TcgDeckOperationResult> RemoveCardAsync(string ownerId, string name, string cardId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var deck = await FindDeckAsync(dbContext, ownerId, name, cancellationToken);
        if (deck is null)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_not_found", name);
        }

        var normalizedId = (cardId ?? string.Empty).Trim().ToLowerInvariant();
        var cards = deck.Cards;
        var index = cards.FindLastIndex(id => id == normalizedId);
        if (index < 0)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_card_not_in_deck", normalizedId);
        }

        var displayName = TcgCardPool.TryGet(normalizedId, out var card) ? card.Name : normalizedId;
        cards.RemoveAt(index);
        deck.Cards = cards;
        await dbContext.SaveChangesAsync(cancellationToken);

        return TcgDeckOperationResult.Ok("tcg_deck_card_removed", deck, displayName, cards.Count,
            TcgDeckConstants.DECK_SIZE);
    }

    public async Task<TcgDeckOperationResult> SetEnergyTypesAsync(string ownerId, string name,
        IReadOnlyList<TcgType> energyTypes, CancellationToken cancellationToken = default)
    {
        if (energyTypes is null || energyTypes.Count < TcgDeckConstants.MIN_ENERGY_TYPES
            || energyTypes.Count > TcgDeckConstants.MAX_ENERGY_TYPES)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_invalid_energy_count",
                TcgDeckConstants.MIN_ENERGY_TYPES, TcgDeckConstants.MAX_ENERGY_TYPES);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var deck = await FindDeckAsync(dbContext, ownerId, name, cancellationToken);
        if (deck is null)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_not_found", name);
        }

        deck.EnergyTypes = energyTypes.Distinct().Select(type => type.ToString()).ToList();
        await dbContext.SaveChangesAsync(cancellationToken);

        return TcgDeckOperationResult.Ok("tcg_deck_energy_set", deck);
    }

    public async Task<TcgDeckOperationResult> DeleteAsync(string ownerId, string name,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var deck = await FindDeckAsync(dbContext, ownerId, name, cancellationToken);
        if (deck is null)
        {
            return TcgDeckOperationResult.Fail("tcg_deck_not_found", name);
        }

        dbContext.TcgDecks.Remove(deck);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TcgDeckOperationResult.Ok("tcg_deck_deleted", null, deck.Name);
    }

    private static Task<TcgDeck> FindDeckAsync(BotDbContext dbContext, string ownerId, string name,
        CancellationToken cancellationToken)
    {
        var normalized = (name ?? string.Empty).Trim().ToLower();
        return dbContext.TcgDecks
            .FirstOrDefaultAsync(deck => deck.OwnerId == ownerId && deck.Name.ToLower() == normalized,
                cancellationToken);
    }
}
