using System.Globalization;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Core.Services.Rooms;

public class RoomFactory : IRoomFactory
{
    private readonly IConfiguration _configuration;
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IDependencyContainerService _dependencyContainerService;

    public RoomFactory(
        IConfiguration configuration,
        IBotDbContextFactory dbContextFactory,
        IDependencyContainerService dependencyContainerService)
    {
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _dependencyContainerService = dependencyContainerService;
    }

    public async Task<IRoom> CreateRoomAsync(string roomId, string[] lines,
        CancellationToken cancellationToken = default)
    {
        var roomTitle = lines
            .FirstOrDefault(x => x.StartsWith("|title|"))
            ?.Split("|")[2] ?? roomId;

        var users = lines
            .FirstOrDefault(x => x.StartsWith("|users|"))?
            .Split("|")[2]
            .Split(",")[1..];

        Log.Information("Initializing {0}...", roomTitle);

        var dbRoomEntity = await InitializeOrUpdateRoomEntity(roomId, roomTitle, cancellationToken);

        var parameterStore = _dependencyContainerService.Resolve<IRoomParameterStore>();
        parameterStore.InitializeFromRoomEntity(dbRoomEntity);
        var localeCode = await parameterStore.GetValueAsync(Parameter.Locale, cancellationToken);
        var timeZoneId = await parameterStore.GetValueAsync(Parameter.TimeZone, cancellationToken);
        var hasTimeZone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone);
        var room = new Room(roomTitle,
            roomId,
            new CultureInfo(localeCode ?? _configuration.DefaultLocaleCode),
            hasTimeZone ? timeZone : TimeZoneInfo.Local,
            parameterStore);

        parameterStore.Room = room;
        room.AddUsers(users ?? []);
        room.InitializeMessageQueueFromLogs(lines);

        Log.Information("Initializing {0} : DONE", roomTitle);

        return room;
    }

    private async Task<SavedRoom> InitializeOrUpdateRoomEntity(string roomId, string roomTitle,
        CancellationToken cancellationToken)
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
        return dbRoom;
    }
}
