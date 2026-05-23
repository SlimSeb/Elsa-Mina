using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ElsaMina.Gui.Pages.Error;

public partial class ExceptionViewModel : ObservableObject
{
    public string Title { get; }
    public string StackTrace { get; }

    public ExceptionViewModel(System.Exception exception)
    {
        Title = exception.GetType().Name + ": " + exception.Message;
        StackTrace = exception.ToString();
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        var clipboard = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } w }
            ? w.Clipboard
            : null;

        if (clipboard != null)
        {
            await clipboard.SetTextAsync(StackTrace);
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
