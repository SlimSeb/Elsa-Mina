using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.DataAccess;

namespace ElsaMina.Commands.Economy;

public class MoneyService : IMoneyService
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IRoomUserDataService _roomUserDataService;

    public MoneyService(IBotDbContextFactory dbContextFactory, IRoomUserDataService roomUserDataService)
    {
        _dbContextFactory = dbContextFactory;
        _roomUserDataService = roomUserDataService;
    }

    public async Task<long> GetBalanceAsync(string roomId, string userId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var roomUser = await dbContext.RoomUsers.FindAsync([userId, roomId], cancellationToken);
        return roomUser?.Money ?? IMoneyService.DEFAULT_BALANCE;
    }

    public async Task<long> AddAsync(string roomId, string userId, long amount,
        CancellationToken cancellationToken = default)
    {
        // Ensures the room user (and its underlying saved user) exists, starting at the default balance.
        await _roomUserDataService.GetOrCreateRoomSpecificUserDataAsync(roomId, userId, cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var roomUser = await dbContext.RoomUsers.FindAsync([userId, roomId], cancellationToken);
        roomUser.Money += amount;
        await dbContext.SaveChangesAsync(cancellationToken);
        return roomUser.Money;
    }
}
