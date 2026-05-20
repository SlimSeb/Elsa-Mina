using System.Collections.Concurrent;
using ElsaMina.Core.Services.CustomColors;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Users.Colors;

public class NameColorsService : INameColorsService, IRoomColorsCache
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public NameColorsService(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var allEntries = await dbContext.NameColors.ToListAsync(cancellationToken);
        foreach (var entry in allEntries)
        {
            _cache[entry.UserId] = entry.Color;
        }
    }

    public string GetColor(string userId) => _cache.GetValueOrDefault(userId);

    public async Task SetColorAsync(string userId, string color, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.NameColors.FindAsync([userId], cancellationToken);
        if (existing != null)
        {
            existing.Color = color;
        }
        else
        {
            await dbContext.NameColors.AddAsync(new NameColor { UserId = userId, Color = color }, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _cache[userId] = color;
    }

    public async Task<bool> DeleteColorAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.NameColors.FindAsync([userId], cancellationToken);
        if (existing == null)
        {
            return false;
        }

        dbContext.NameColors.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
        _cache.TryRemove(userId, out _);
        return true;
    }
}
