using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Gui.Services.Bot;

public interface IBotService
{
    BotStatus Status { get; }
    bool IsRunning { get; }
    TimeSpan Uptime { get; }
    IReadOnlyList<string> ConfiguredRooms { get; }
    string? ErrorMessage { get; }
    IReadOnlyDictionary<Parameter, IParameterDefinition>? ParameterDefinitions { get; }

    event Action<BotStatus>? StatusChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    IRoom? GetRoom(string roomId);
}
