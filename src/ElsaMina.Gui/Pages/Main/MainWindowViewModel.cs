using CommunityToolkit.Mvvm.ComponentModel;
using ElsaMina.Gui.Pages.Dashboard;
using ElsaMina.Gui.Pages.Room;
using ElsaMina.Gui.Pages.Setup;
using ElsaMina.Gui.Services.BotConfiguration;

namespace ElsaMina.Gui.Pages.Main;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly SetupViewModel _setupViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly RoomViewModel _roomViewModel;

    [ObservableProperty]
    private object _currentViewModel = null!;

    public MainWindowViewModel(
        IConfigurationFileService configFileService,
        SetupViewModel setupViewModel,
        DashboardViewModel dashboardViewModel,
        RoomViewModel roomViewModel)
    {
        _setupViewModel = setupViewModel;
        _dashboardViewModel = dashboardViewModel;
        _roomViewModel = roomViewModel;

        _setupViewModel.SetupCompleted += NavigateToDashboard;
        _setupViewModel.GoBackRequested += NavigateToDashboard;
        _dashboardViewModel.SetupRequested += NavigateToSetup;
        _dashboardViewModel.RoomSelected += NavigateToRoom;
        _roomViewModel.GoBackRequested += NavigateToDashboard;

        if (configFileService.ConfigExists)
        {
            _ = LoadAndNavigateToDashboardAsync();
        }
        else
        {
            CurrentViewModel = _setupViewModel;
        }
    }

    private async Task LoadAndNavigateToDashboardAsync()
    {
        await _setupViewModel.LoadFromFileAsync();
        CurrentViewModel = _dashboardViewModel;
    }

    private void NavigateToDashboard() => CurrentViewModel = _dashboardViewModel;

    private async void NavigateToSetup()
    {
        await _setupViewModel.LoadFromFileAsync();
        CurrentViewModel = _setupViewModel;
    }

    private async void NavigateToRoom(string roomId)
    {
        await _roomViewModel.LoadAsync(roomId);
        CurrentViewModel = _roomViewModel;
    }
}
