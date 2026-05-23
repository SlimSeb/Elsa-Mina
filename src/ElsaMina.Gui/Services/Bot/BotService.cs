using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Autofac;
using ElsaMina.Battles;
using ElsaMina.Commands;
using ElsaMina.Core;
using ElsaMina.Core.Modules;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.FileSharing.S3;
using ElsaMina.Gui.Services.BotConfiguration;
using ElsaMina.Gui.Services.Version;
using ElsaMina.Logging;

namespace ElsaMina.Gui.Services.Bot;

public class BotService : IBotService
{
    private readonly IConfigurationFileService _configFileService;
    private IContainer? _container;
    private IBot? _bot;
    private IClient? _client;
    private IDisposable? _messageSubscription;

    public BotStatus Status { get; private set; } = BotStatus.Stopped;
    public bool IsRunning => Status == BotStatus.Running;
    public TimeSpan Uptime => _bot?.UpTime ?? TimeSpan.Zero;
    public IReadOnlyList<string> ConfiguredRooms { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public event Action<BotStatus>? StatusChanged;

    public BotService(IConfigurationFileService configFileService)
    {
        _configFileService = configFileService;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Status is BotStatus.Running or BotStatus.Starting)
        {
            return;
        }

        SetStatus(BotStatus.Starting);
        ErrorMessage = null;

        try
        {
            var config = await _configFileService.LoadConfigurationAsync();
            Log.Configuration = config;
            ConfiguredRooms = config.Rooms?.ToList() ?? [];

            var builder = new ContainerBuilder();
            builder.RegisterInstance(config)
                .As<IConfiguration>()
                .As<IS3CredentialsProvider>()
                .SingleInstance();
            builder.RegisterModule<CoreModule>();
            builder.RegisterModule<BattlesModule>();
            builder.RegisterModule<CommandModule>();
            builder.RegisterType<VersionProvider>().As<IVersionProvider>();

            _container = await Task.Run(() => builder.Build(), cancellationToken);

            var dependencyContainerService = _container.Resolve<IDependencyContainerService>();
            dependencyContainerService.SetContainer(_container);
            DependencyContainerService.Current = dependencyContainerService;

            _bot = dependencyContainerService.Resolve<IBot>();
            _client = dependencyContainerService.Resolve<IClient>();

            _messageSubscription = _client.MessageReceived
                .Select(message => _bot.HandleReceivedMessageAsync(message).ToObservable())
                .Concat()
                .Catch((Exception ex) =>
                {
                    Log.Error(ex, "Error while handling message");
                    return Observable.Throw<Unit>(ex);
                })
                .Subscribe();

            _client.DisconnectionHappened.Subscribe(info =>
            {
                Log.Warning("Disconnected. Type: {0}", info.Type);
                _bot?.OnDisconnect();
            });

            _client.ReconnectionHappened.Subscribe(info =>
            {
                Log.Warning("Reconnecting: {0}", info.Type);
                _bot?.OnReconnect();
            });

            await _bot.StartAsync();
            SetStatus(BotStatus.Running);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Log.Error(ex, "Failed to start bot");
            SetStatus(BotStatus.Error);
            await CleanupAsync();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Status is BotStatus.Stopped or BotStatus.Stopping)
        {
            return;
        }

        SetStatus(BotStatus.Stopping);

        try
        {
            _bot?.OnExit();
            if (_client != null)
            {
                await _client.Close();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while stopping bot");
        }
        finally
        {
            await CleanupAsync();
            SetStatus(BotStatus.Stopped);
        }
    }

    private Task CleanupAsync()
    {
        _messageSubscription?.Dispose();
        _messageSubscription = null;
        _bot = null;
        _client = null;
        _container?.Dispose();
        _container = null;
        return Task.CompletedTask;
    }

    private void SetStatus(BotStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }
}
