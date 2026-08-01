using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Core.Services.Repeats;

public class RepeatsManager : IRepeatsManager
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly Lazy<IBot> _bot;
    private readonly Dictionary<Guid, Repeat> _repeats = new();

    public RepeatsManager(IBotDbContextFactory dbContextFactory, Lazy<IBot> bot)
    {
        _dbContextFactory = dbContextFactory;
        _bot = bot;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var savedRepeats = await dbContext.Repeats.ToListAsync(cancellationToken);
        foreach (var savedRepeat in savedRepeats)
        {
            StartTimer(savedRepeat.Id, savedRepeat.RoomId, savedRepeat.Message, savedRepeat.Interval);
        }
    }

    public async Task StartRepeatAsync(string roomId, string message, TimeSpan interval,
        CancellationToken cancellationToken = default)
    {
        var repeatId = Guid.NewGuid();
        await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            dbContext.Repeats.Add(new SavedRepeat
            {
                Id = repeatId,
                RoomId = roomId,
                Message = message,
                Interval = interval
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        StartTimer(repeatId, roomId, message, interval);
    }

    public IRepeat GetRepeat(Guid repeatId)
    {
        return _repeats.GetValueOrDefault(repeatId);
    }

    public IEnumerable<IRepeat> GetRepeats(string roomId)
    {
        return _repeats.Values.Where(repeat => repeat.RoomId == roomId);
    }

    public async Task<bool> StopRepeatAsync(Guid repeatId, CancellationToken cancellationToken = default)
    {
        var stopped = _repeats.Remove(repeatId, out var repeat);
        repeat?.Stop();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var savedRepeat = await dbContext.Repeats.FindAsync([repeatId], cancellationToken);
        if (savedRepeat != null)
        {
            dbContext.Repeats.Remove(savedRepeat);
            await dbContext.SaveChangesAsync(cancellationToken);
            stopped = true;
        }

        return stopped;
    }

    private void StartTimer(Guid repeatId, string roomId, string message, TimeSpan interval)
    {
        var repeat = new Repeat(_bot.Value, repeatId, roomId, message, interval);
        _repeats[repeatId] = repeat;
        repeat.Start();
    }
}
