using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.DataAccess;

public class EfRoomParameterRepository : IRoomParameterRepository
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public EfRoomParameterRepository(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadOrCreateRoomAsync(string roomId, string roomTitle,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dbRoom = await dbContext
            .RoomInfo
            .Include(savedRoom => savedRoom.ParameterValues)
            .FirstOrDefaultAsync(savedRoom => savedRoom.Id == roomId, cancellationToken);

        if (dbRoom == null)
        {
            Log.Information("Could not find room parameters, inserting...");
            dbRoom = new SavedRoom { Id = roomId, Title = roomTitle };
            await dbContext.RoomInfo.AddAsync(dbRoom, cancellationToken);
            Log.Information("Inserted room parameters for {0}", roomId);
        }
        else
        {
            dbRoom.Title = roomTitle;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return dbRoom.ParameterValues.ToDictionary(parameterValue => parameterValue.ParameterId,
            parameterValue => parameterValue.Value);
    }

    public async Task<bool> TrySaveParameterValueAsync(string roomId, string parameterIdentifier, string value,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await dbContext.RoomBotParameterValues.FindAsync([roomId, parameterIdentifier],
                cancellationToken);
            if (existing == null)
            {
                dbContext.RoomBotParameterValues.Add(new RoomBotParameterValue
                {
                    RoomId = roomId,
                    ParameterId = parameterIdentifier,
                    Value = value
                });
            }
            else
            {
                existing.Value = value;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
        {
            Log.Error(exception, "Failed to set room parameter value for RoomId={RoomId}, Parameter={Parameter}",
                roomId, parameterIdentifier);
            return false;
        }
    }
}
