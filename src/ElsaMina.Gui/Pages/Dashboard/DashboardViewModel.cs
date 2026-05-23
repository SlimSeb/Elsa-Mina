using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElsaMina.Gui.Services.Bot;
using ElsaMina.Logging;

namespace ElsaMina.Gui.Pages.Dashboard;

public partial class DashboardViewModel : ObservableObject
{
    private const int MAX_LOG_ENTRIES = 500;

    private readonly IBotService _botService;
    private readonly DispatcherTimer _uptimeTimer;

    public event Action? SetupRequested;
    public event Action? LogsUpdated;

    public ObservableCollection<LogEntryViewModel> LogEntries { get; } = [];
    public ObservableCollection<string> ConfiguredRooms { get; } = [];

    [ObservableProperty] private string _statusText = "Stopped";
    [ObservableProperty] private string _statusColor = "#757575";
    [ObservableProperty] private string _uptime = "--:--:--";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _hasError;

    public DashboardViewModel(IBotService botService)
    {
        _botService = botService;
        _botService.StatusChanged += OnStatusChanged;

        GuiSink.OnLogEmitted = OnLogEmitted;

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uptimeTimer.Tick += (_, _) => UpdateUptime();
        _uptimeTimer.Start();

        RefreshStatus(_botService.Status);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() => _botService.StartAsync();

    private bool CanStart() => !_botService.IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => _botService.StopAsync();

    private bool CanStop() => _botService.IsRunning;

    [RelayCommand]
    private void ClearLogs() => LogEntries.Clear();

    [RelayCommand]
    private void OpenSetup() => SetupRequested?.Invoke();

    private void OnStatusChanged(BotStatus status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RefreshStatus(status);
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();

            if (status == BotStatus.Running)
            {
                ConfiguredRooms.Clear();
                foreach (var room in _botService.ConfiguredRooms)
                {
                    ConfiguredRooms.Add(room);
                }
            }
            else if (status is BotStatus.Stopped or BotStatus.Error)
            {
                ConfiguredRooms.Clear();
            }
        });
    }

    private void RefreshStatus(BotStatus status)
    {
        StatusText = status switch
        {
            BotStatus.Running => "Running",
            BotStatus.Starting => "Starting...",
            BotStatus.Stopping => "Stopping...",
            BotStatus.Error => "Error",
            _ => "Stopped"
        };

        StatusColor = status switch
        {
            BotStatus.Running => "#66BB6A",
            BotStatus.Starting or BotStatus.Stopping => "#FFA726",
            BotStatus.Error => "#EF5350",
            _ => "#757575"
        };

        ErrorMessage = _botService.ErrorMessage;
        HasError = status == BotStatus.Error;
    }

    private void UpdateUptime()
    {
        if (!_botService.IsRunning)
        {
            Uptime = "--:--:--";
            return;
        }

        var uptime = _botService.Uptime;
        Uptime = uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}"
            : $"{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }

    private void OnLogEmitted(LogLevel level, string message, DateTime timestamp)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (LogEntries.Count >= MAX_LOG_ENTRIES)
            {
                LogEntries.RemoveAt(0);
            }

            LogEntries.Add(new LogEntryViewModel(level, message, timestamp));
            LogsUpdated?.Invoke();
        });
    }
}
