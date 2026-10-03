using System.Globalization;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Rooms;

public class RoomFactory : IRoomFactory
{
    private readonly IConfiguration _configuration;
    private readonly IRoomParameterRepository _roomParameterRepository;
    private readonly Func<IRoomParameterStore> _parameterStoreFactory;

    public RoomFactory(
        IConfiguration configuration,
        IRoomParameterRepository roomParameterRepository,
        Func<IRoomParameterStore> parameterStoreFactory)
    {
        _configuration = configuration;
        _roomParameterRepository = roomParameterRepository;
        _parameterStoreFactory = parameterStoreFactory;
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

        var storedValues =
            await _roomParameterRepository.LoadOrCreateRoomAsync(roomId, roomTitle, cancellationToken);

        var parameterStore = _parameterStoreFactory();
        parameterStore.Initialize(roomId, storedValues);
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
}
