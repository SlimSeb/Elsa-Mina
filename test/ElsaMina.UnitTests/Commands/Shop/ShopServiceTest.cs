using ElsaMina.Commands.Shop;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Shop;

[TestFixture]
public class ShopServiceTest
{
    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _dbContextFactory;
    private ShopService _shopService;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));

        _shopService = new ShopService(_dbContextFactory);
    }

    private async Task SeedItemAsync(string tier, string article, string price)
    {
        await using var db = new BotDbContext(_options);
        db.ShopItems.Add(new ShopItem { Tier = tier, Article = article, Price = price });
        await db.SaveChangesAsync();
    }

    private async Task<List<ShopItem>> GetAllItemsAsync()
    {
        await using var db = new BotDbContext(_options);
        return await db.ShopItems.OrderBy(i => i.Id).ToListAsync();
    }

    // GetShopDataAsync

    [Test]
    public async Task Test_GetShopDataAsync_ShouldReturnEmptyDictionary_WhenNoItemsExist()
    {
        // Act
        var result = await _shopService.GetShopDataAsync();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_GetShopDataAsync_ShouldGroupItemsByTier_WhenItemsExist()
    {
        // Arrange
        await SeedItemAsync("1", "Article A", "100");
        await SeedItemAsync("2", "Article B", "200");
        await SeedItemAsync("1", "Article C", "150");

        // Act
        var result = await _shopService.GetShopDataAsync();

        // Assert
        Assert.That(result.Keys, Has.Count.EqualTo(2));
        Assert.That(result["1"], Has.Count.EqualTo(2));
        Assert.That(result["2"], Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Test_GetShopDataAsync_ShouldOrderItemsById_WhenMultipleItemsExist()
    {
        // Arrange
        await SeedItemAsync("1", "Article B", "200");
        await SeedItemAsync("1", "Article A", "100");

        // Act
        var result = await _shopService.GetShopDataAsync();

        // Assert
        var tier1 = result["1"];
        Assert.That(tier1[0].Article, Is.EqualTo("Article B"));
        Assert.That(tier1[1].Article, Is.EqualTo("Article A"));
    }

    // AddItemAsync

    [Test]
    public async Task Test_AddItemAsync_ShouldPersistItem_WhenCalled()
    {
        // Act
        await _shopService.AddItemAsync("1", "New Article", "500");

        // Assert
        var items = await GetAllItemsAsync();
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Tier, Is.EqualTo("1"));
        Assert.That(items[0].Article, Is.EqualTo("New Article"));
        Assert.That(items[0].Price, Is.EqualTo("500"));
    }

    [Test]
    public async Task Test_AddItemAsync_ShouldReturnSavedItem_WhenCalled()
    {
        // Act
        var result = await _shopService.AddItemAsync("2", "My Item", "300");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Tier, Is.EqualTo("2"));
        Assert.That(result.Article, Is.EqualTo("My Item"));
        Assert.That(result.Price, Is.EqualTo("300"));
        Assert.That(result.Id, Is.GreaterThan(0));
    }

    // UpdateItemAsync

    [Test]
    public async Task Test_UpdateItemAsync_ShouldReturnNull_WhenItemDoesNotExist()
    {
        // Act
        var result = await _shopService.UpdateItemAsync(999, "New Name", "999");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_UpdateItemAsync_ShouldUpdateArticleAndPrice_WhenItemExists()
    {
        // Arrange
        await SeedItemAsync("1", "Old Name", "100");
        var items = await GetAllItemsAsync();
        var id = items[0].Id;

        // Act
        var result = await _shopService.UpdateItemAsync(id, "New Name", "999");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Article, Is.EqualTo("New Name"));
        Assert.That(result.Price, Is.EqualTo("999"));

        var persisted = await GetAllItemsAsync();
        Assert.That(persisted[0].Article, Is.EqualTo("New Name"));
        Assert.That(persisted[0].Price, Is.EqualTo("999"));
    }

    [Test]
    public async Task Test_UpdateItemAsync_ShouldNotChangeTier_WhenUpdatingItem()
    {
        // Arrange
        await SeedItemAsync("2", "Old Name", "100");
        var items = await GetAllItemsAsync();
        var id = items[0].Id;

        // Act
        await _shopService.UpdateItemAsync(id, "New Name", "999");

        // Assert
        var persisted = await GetAllItemsAsync();
        Assert.That(persisted[0].Tier, Is.EqualTo("2"));
    }

    // RemoveItemAsync

    [Test]
    public async Task Test_RemoveItemAsync_ShouldReturnFalse_WhenItemDoesNotExist()
    {
        // Act
        var result = await _shopService.RemoveItemAsync("1", "Ghost Item");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Test_RemoveItemAsync_ShouldReturnFalse_WhenTierDoesNotMatch()
    {
        // Arrange
        await SeedItemAsync("1", "Article A", "100");

        // Act
        var result = await _shopService.RemoveItemAsync("2", "Article A");

        // Assert
        Assert.That(result, Is.False);
        Assert.That(await GetAllItemsAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Test_RemoveItemAsync_ShouldDeleteItem_WhenItemExists()
    {
        // Arrange
        await SeedItemAsync("1", "Article A", "100");

        // Act
        var result = await _shopService.RemoveItemAsync("1", "Article A");

        // Assert
        Assert.That(result, Is.True);
        Assert.That(await GetAllItemsAsync(), Is.Empty);
    }

    [Test]
    public async Task Test_RemoveItemAsync_ShouldOnlyDeleteMatchingItem_WhenMultipleItemsExist()
    {
        // Arrange
        await SeedItemAsync("1", "Article A", "100");
        await SeedItemAsync("1", "Article B", "200");

        // Act
        await _shopService.RemoveItemAsync("1", "Article A");

        // Assert
        var remaining = await GetAllItemsAsync();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].Article, Is.EqualTo("Article B"));
    }
}
