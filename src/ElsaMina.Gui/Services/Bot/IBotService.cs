namespace ElsaMina.Gui.Services.Bot;

public interface IBotService
{
    BotStatus Status { get; }
    bool IsRunning { get; }
    TimeSpan Uptime { get; }
    IReadOnlyList<string> ConfiguredRooms { get; }
    string? ErrorMessage { get; }

    event Action<BotStatus>? StatusChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
