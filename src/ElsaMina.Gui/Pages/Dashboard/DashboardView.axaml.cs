using Avalonia.Controls;
using Avalonia.Threading;

namespace ElsaMina.Gui.Pages.Dashboard;

public partial class DashboardView : UserControl
{
    private ScrollViewer? _logScrollViewer;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.LogsUpdated += ScrollLogsToBottom;
        }
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _logScrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
    }

    private void ScrollLogsToBottom()
    {
        Dispatcher.UIThread.Post(() => _logScrollViewer?.ScrollToEnd(), DispatcherPriority.Background);
    }
}
