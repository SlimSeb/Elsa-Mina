using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElsaMina.Gui.Services.Bot;

namespace ElsaMina.Gui.Pages.Room;

public partial class RoomViewModel : ObservableObject
{
    private readonly IBotService _botService;

    public event Action? GoBackRequested;

    public ObservableCollection<RoomUserViewModel> Users { get; } = [];
    public ObservableCollection<RoomParameterViewModel> Parameters { get; } = [];

    [ObservableProperty] private string _roomId = string.Empty;
    [ObservableProperty] private string _roomName = string.Empty;
    [ObservableProperty] private int _userCount;
    [ObservableProperty] private bool _isLoading;

    public RoomViewModel(IBotService botService)
    {
        _botService = botService;
    }

    [RelayCommand]
    private void GoBack() => GoBackRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync() => await LoadAsync(RoomId);

    private bool CanRefresh() => !IsLoading;

    public async Task LoadAsync(string roomId)
    {
        RoomId = roomId;
        IsLoading = true;
        RefreshCommand.NotifyCanExecuteChanged();
        Users.Clear();
        Parameters.Clear();

        try
        {
            var room = _botService.GetRoom(roomId);
            if (room == null)
            {
                RoomName = roomId;
                return;
            }

            RoomName = room.Name;

            var sortedUsers = room.Users.Values
                .OrderByDescending(u => u.Rank)
                .ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var user in sortedUsers)
            {
                Users.Add(new RoomUserViewModel(user));
            }

            UserCount = Users.Count;

            var paramDefs = _botService.ParameterDefinitions;
            if (paramDefs != null)
            {
                foreach (var (param, def) in paramDefs)
                {
                    var paramVm = new RoomParameterViewModel(param, def);
                    var value = await room.GetParameterValueAsync(param).ConfigureAwait(false);
                    paramVm.Value = value ?? def.DefaultValue;
                    Parameters.Add(paramVm);
                }
            }
        }
        finally
        {
            IsLoading = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }
}
