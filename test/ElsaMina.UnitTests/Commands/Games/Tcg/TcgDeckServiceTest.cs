using ElsaMina.Commands.Games.Tcg.Cards;
using ElsaMina.Commands.Games.Tcg.Decks;
using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Tcg;

public class TcgDeckServiceTest
{
    private const string Owner = "owner";

    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IClockService _clockService;
    private TcgDeckService _sut;

    private string _cardId;
    private string _otherCardId;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTime.Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        _sut = new TcgDeckService(_dbContextFactory, _clockService);

        _cardId = TcgCardPool.AllCards[0].Id;
        _otherCardId = TcgCardPool.AllCards[1].Id;
    }

    [Test]
    public async Task Test_CreateAsync_ShouldCreateDeck()
    {
        var result = await _sut.CreateAsync(Owner, "My Deck");

        await using var dbContext = new BotDbContext(_dbOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(await dbContext.TcgDecks.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_CreateAsync_ShouldFail_WhenNameDuplicatedIgnoringCase()
    {
        await _sut.CreateAsync(Owner, "My Deck");

        var result = await _sut.CreateAsync(Owner, "my deck");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.MessageKey, Is.EqualTo("tcg_deck_already_exists"));
        }
    }

    [Test]
    public async Task Test_CreateAsync_ShouldFail_WhenNameContainsComma()
    {
        var result = await _sut.CreateAsync(Owner, "bad,name");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.MessageKey, Is.EqualTo("tcg_deck_name_no_comma"));
        }
    }

    [Test]
    public async Task Test_AddCardAsync_ShouldAddCardAndReturnUpdatedDeck()
    {
        await _sut.CreateAsync(Owner, "deck");

        var result = await _sut.AddCardAsync(Owner, "deck", _cardId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Deck.Cards, Has.Count.EqualTo(1));
            Assert.That(result.Deck.Cards[0], Is.EqualTo(_cardId));
        }
    }

    [Test]
    public async Task Test_AddCardAsync_ShouldFail_WhenUnknownCard()
    {
        await _sut.CreateAsync(Owner, "deck");

        var result = await _sut.AddCardAsync(Owner, "deck", "not-a-real-card");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.MessageKey, Is.EqualTo("tcg_deck_unknown_card"));
        }
    }

    [Test]
    public async Task Test_AddCardAsync_ShouldEnforceMaxCopies()
    {
        await _sut.CreateAsync(Owner, "deck");
        await _sut.AddCardAsync(Owner, "deck", _cardId);
        await _sut.AddCardAsync(Owner, "deck", _cardId);

        var result = await _sut.AddCardAsync(Owner, "deck", _cardId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.MessageKey, Is.EqualTo("tcg_deck_max_copies"));
        }
    }

    [Test]
    public async Task Test_AddCardAsync_ShouldFail_WhenDeckFull()
    {
        await SeedFullDeckAsync("deck");

        var result = await _sut.AddCardAsync(Owner, "deck", _cardId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.MessageKey, Is.EqualTo("tcg_deck_full"));
        }
    }

    [Test]
    public async Task Test_RemoveCardAsync_ShouldRemoveOneCopy()
    {
        await _sut.CreateAsync(Owner, "deck");
        await _sut.AddCardAsync(Owner, "deck", _cardId);
        await _sut.AddCardAsync(Owner, "deck", _cardId);

        var result = await _sut.RemoveCardAsync(Owner, "deck", _cardId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Deck.Cards, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_RemoveCardAsync_ShouldFail_WhenCardNotInDeck()
    {
        await _sut.CreateAsync(Owner, "deck");

        var result = await _sut.RemoveCardAsync(Owner, "deck", _otherCardId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.MessageKey, Is.EqualTo("tcg_deck_card_not_in_deck"));
        }
    }

    [Test]
    public async Task Test_SetEnergyTypesAsync_ShouldStoreTypes()
    {
        await _sut.CreateAsync(Owner, "deck");

        var result = await _sut.SetEnergyTypesAsync(Owner, "deck", [TcgType.Fire, TcgType.Water]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Deck.EnergyTypes,
                Is.EquivalentTo([TcgType.Fire.ToString(), TcgType.Water.ToString()]));
        }
    }

    [Test]
    public async Task Test_SetEnergyTypesAsync_ShouldFail_WhenTooManyTypes()
    {
        await _sut.CreateAsync(Owner, "deck");

        var result = await _sut.SetEnergyTypesAsync(Owner, "deck",
            [TcgType.Fire, TcgType.Water, TcgType.Grass, TcgType.Psychic]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.MessageKey, Is.EqualTo("tcg_deck_invalid_energy_count"));
        }
    }

    [Test]
    public async Task Test_DeleteAsync_ShouldRemoveDeck()
    {
        await _sut.CreateAsync(Owner, "deck");

        var result = await _sut.DeleteAsync(Owner, "deck");

        await using var dbContext = new BotDbContext(_dbOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(await dbContext.TcgDecks.CountAsync(), Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Test_GetUserDecksAsync_ShouldReturnOnlyOwnerDecks()
    {
        await _sut.CreateAsync(Owner, "deck1");
        await _sut.CreateAsync(Owner, "deck2");
        await _sut.CreateAsync("someone-else", "deck3");

        var decks = await _sut.GetUserDecksAsync(Owner);

        Assert.That(decks.Select(deck => deck.Name), Is.EquivalentTo(["deck1", "deck2"]));
    }

    private async Task SeedFullDeckAsync(string name)
    {
        var cards = TcgCardPool.AllCards
            .Take(10)
            .SelectMany(card => new[] { card.Id, card.Id })
            .ToList();

        await using var dbContext = new BotDbContext(_dbOptions);
        dbContext.TcgDecks.Add(new TcgDeck
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = Owner,
            Name = name,
            Cards = cards,
            CreationDate = _clockService.CurrentUtcDateTime
        });
        await dbContext.SaveChangesAsync();
    }
}
