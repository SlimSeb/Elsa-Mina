using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Shop;

public class ShopService : IShopService
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public ShopService(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Dictionary<string, List<ShopItem>>> GetShopDataAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var items = await dbContext.ShopItems.ToListAsync();
        return items
            .GroupBy(item => item.Tier)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public async Task<ShopItem> AddItemAsync(string tier, string article, string price,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var shopItem = new ShopItem { Tier = tier, Article = article, Price = price };
        await dbContext.ShopItems.AddAsync(shopItem, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return shopItem;
    }

    public async Task<ShopItem> UpdateItemAsync(int id, string article, string price,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var shopItem = await dbContext.ShopItems.FindAsync([id], cancellationToken);
        if (shopItem == null)
        {
            return null;
        }

        shopItem.Article = article;
        shopItem.Price = price;
        await dbContext.SaveChangesAsync(cancellationToken);
        return shopItem;
    }

    public async Task<bool> RemoveItemAsync(string tier, string article,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var shopItem = await dbContext.ShopItems
            .FirstOrDefaultAsync(item => item.Tier == tier && item.Article == article, cancellationToken);
        if (shopItem == null)
        {
            return false;
        }

        dbContext.ShopItems.Remove(shopItem);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
