using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Tournaments.Betting;

public class BetRecordsStore : IBetRecordsStore
{
    private readonly IBotDbContextFactory _botDbContextFactory;
    private readonly IRoomUserDataService _roomUserDataService;

    public BetRecordsStore(IBotDbContextFactory botDbContextFactory, IRoomUserDataService roomUserDataService)
    {
        _botDbContextFactory = botDbContextFactory;
        _roomUserDataService = roomUserDataService;
    }

    public async Task SaveBetOutcomesAsync(string roomId, IEnumerable<string> bettorIds,
        IReadOnlySet<string> correctBettorIds, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _botDbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var bettorId in bettorIds)
        {
            await _roomUserDataService.GetOrCreateRoomSpecificUserDataAsync(roomId, bettorId, cancellationToken);

            var record = await dbContext.BetRecords.FindAsync([bettorId, roomId], cancellationToken);
            if (record == null)
            {
                record = new BetRecord { UserId = bettorId, RoomId = roomId };
                await dbContext.BetRecords.AddAsync(record, cancellationToken);
            }

            record.TotalBetsCount++;
            if (correctBettorIds.Contains(bettorId))
            {
                record.CorrectBetsCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
