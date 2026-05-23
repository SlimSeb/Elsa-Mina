using Autofac;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ElsaMina.Gui.Pages.Dashboard;
using ElsaMina.Gui.Pages.Error;
using ElsaMina.Gui.Pages.Main;
using ElsaMina.Gui.Pages.Setup;
using ElsaMina.Gui.Services.Bot;
using ElsaMina.Gui.Services.BotConfiguration;

namespace ElsaMina.Gui;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            RegisterGlobalExceptionHandlers(desktop);

            try
            {
                var container = BuildContainer();
                var mainWindowVm = container.Resolve<MainWindowViewModel>();

                var mainWindow = new MainWindow { DataContext = mainWindowVm };
                desktop.MainWindow = mainWindow;

                desktop.Exit += (_, _) =>
                {
                    var botService = container.Resolve<IBotService>();
                    if (botService.IsRunning)
                    {
                        botService.StopAsync().GetAwaiter().GetResult();
                    }
                    container.Dispose();
                };
            }
            catch (System.Exception ex)
            {
                ShowExceptionWindow(desktop, ex);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterGlobalExceptionHandlers(IClassicDesktopStyleApplicationLifetime desktop)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as System.Exception
                     ?? new System.Exception(args.ExceptionObject?.ToString() ?? "Unknown error");
            Dispatcher.UIThread.Post(() => ShowExceptionWindow(desktop, ex));
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            Dispatcher.UIThread.Post(() => ShowExceptionWindow(desktop, args.Exception));
        };

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            args.Handled = true;
            ShowExceptionWindow(desktop, args.Exception);
        };
    }

    private static void ShowExceptionWindow(IClassicDesktopStyleApplicationLifetime desktop, System.Exception ex)
    {
        var window = new ExceptionWindow { DataContext = new ExceptionViewModel(ex) };

        if (desktop.MainWindow != null)
        {
            window.ShowDialog(desktop.MainWindow);
        }
        else
        {
            desktop.MainWindow = window;
            window.Show();
        }
    }

    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<ConfigurationFileService>().As<IConfigurationFileService>().SingleInstance();
        builder.RegisterType<BotService>().As<IBotService>().SingleInstance();

        builder.RegisterType<SetupViewModel>().SingleInstance();
        builder.RegisterType<DashboardViewModel>().SingleInstance();
        builder.RegisterType<MainWindowViewModel>().SingleInstance();

        return builder.Build();
    }
}
