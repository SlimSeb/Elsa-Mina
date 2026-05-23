using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElsaMina.Core.Services.Config;
using ElsaMina.Gui.Services.BotConfiguration;

namespace ElsaMina.Gui.Pages.Setup;

public partial class SetupViewModel : ObservableObject
{
    private readonly IConfigurationFileService _configFileService;

    public event Action? SetupCompleted;
    public event Action? GoBackRequested;

    public static IReadOnlyList<string> AvailableLocales { get; } =
        ["en-US", "fr-FR", "es-ES", "it-IT", "pt-BR", "de-DE"];

    [ObservableProperty] private string _host = "sim.smogon.com";
    [ObservableProperty] private string _port = "8000";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _trigger = "-";
    [ObservableProperty] private string _avatar = string.Empty;
    [ObservableProperty] private string _roomsText = string.Empty;
    [ObservableProperty] private string _defaultRoom = string.Empty;
    [ObservableProperty] private string _defaultLocaleCode = "en-US";
    [ObservableProperty] private string _connectionString = string.Empty;
    [ObservableProperty] private string _youtubeApiKey = string.Empty;
    [ObservableProperty] private string _tenorApiKey = string.Empty;
    [ObservableProperty] private string _mistralApiKey = string.Empty;
    [ObservableProperty] private string _chatGptApiKey = string.Empty;
    [ObservableProperty] private string _geminiApiKey = string.Empty;
    [ObservableProperty] private string _elevenLabsApiKey = string.Empty;
    [ObservableProperty] private string _dictionaryApiKey = string.Empty;
    [ObservableProperty] private string _geniusApiKey = string.Empty;
    [ObservableProperty] private string _unsplashApiKey = string.Empty;
    [ObservableProperty] private string _riotApiKey = string.Empty;
    [ObservableProperty] private string _spoonacularApiKey = string.Empty;
    [ObservableProperty] private string _lokiUrl = string.Empty;
    [ObservableProperty] private string _lokiUser = string.Empty;
    [ObservableProperty] private string _lokiApiKey = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _canGoBack;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    public SetupViewModel(IConfigurationFileService configFileService)
    {
        _configFileService = configFileService;
    }

    [RelayCommand]
    private void GoBack() => GoBackRequested?.Invoke();

    public async Task LoadFromFileAsync()
    {
        CanGoBack = _configFileService.ConfigExists;
        var config = await _configFileService.LoadConfigurationAsync();
        Host = config.Host ?? "sim.smogon.com";
        Port = config.Port ?? "8000";
        Name = config.Name ?? string.Empty;
        Password = config.Password ?? string.Empty;
        Trigger = config.Trigger ?? "-";
        Avatar = config.Avatar ?? string.Empty;
        RoomsText = config.Rooms != null ? string.Join(", ", config.Rooms) : string.Empty;
        DefaultRoom = config.DefaultRoom ?? string.Empty;
        DefaultLocaleCode = config.DefaultLocaleCode ?? "en-US";
        ConnectionString = config.ConnectionString ?? string.Empty;
        YoutubeApiKey = config.YoutubeApiKey ?? string.Empty;
        TenorApiKey = config.TenorApiKey ?? string.Empty;
        MistralApiKey = config.MistralApiKey ?? string.Empty;
        ChatGptApiKey = config.ChatGptApiKey ?? string.Empty;
        GeminiApiKey = config.GeminiApiKey ?? string.Empty;
        ElevenLabsApiKey = config.ElevenLabsApiKey ?? string.Empty;
        DictionaryApiKey = config.DictionaryApiKey ?? string.Empty;
        GeniusApiKey = config.GeniusApiKey ?? string.Empty;
        UnsplashApiKey = config.UnsplashApiKey ?? string.Empty;
        RiotApiKey = config.RiotApiKey ?? string.Empty;
        SpoonacularApiKey = config.SpoonacularApiKey ?? string.Empty;
        LokiUrl = config.LokiUrl ?? string.Empty;
        LokiUser = config.LokiUser ?? string.Empty;
        LokiApiKey = config.LokiApiKey ?? string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Host and Username are required.";
            return;
        }

        IsSaving = true;
        try
        {
            var rooms = RoomsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var config = new Configuration
            {
                Host = Host,
                Port = Port,
                Name = Name,
                Password = Password,
                Trigger = string.IsNullOrWhiteSpace(Trigger) ? "-" : Trigger,
                Avatar = Avatar,
                Rooms = rooms,
                DefaultRoom = DefaultRoom,
                DefaultLocaleCode = DefaultLocaleCode,
                ConnectionString = ConnectionString,
                DatabaseMaxRetries = 3,
                DatabaseRetryDelay = TimeSpan.FromSeconds(5),
                LoginRetryDelay = TimeSpan.FromSeconds(5),
                PlayTimeUpdatesInterval = TimeSpan.FromMinutes(1),
                UserUpdateBatchSize = 100,
                UserUpdateFlushInterval = TimeSpan.FromSeconds(30),
                YoutubeApiKey = YoutubeApiKey,
                TenorApiKey = TenorApiKey,
                MistralApiKey = MistralApiKey,
                ChatGptApiKey = ChatGptApiKey,
                GeminiApiKey = GeminiApiKey,
                ElevenLabsApiKey = ElevenLabsApiKey,
                DictionaryApiKey = DictionaryApiKey,
                GeniusApiKey = GeniusApiKey,
                UnsplashApiKey = UnsplashApiKey,
                RiotApiKey = RiotApiKey,
                SpoonacularApiKey = SpoonacularApiKey,
                LokiUrl = LokiUrl,
                LokiUser = LokiUser,
                LokiApiKey = LokiApiKey,
                DiscordWebhooks = new Dictionary<string, string>(),
                TourAnnounces = new Dictionary<string, IEnumerable<string>>()
            };

            await _configFileService.SaveConfigurationAsync(config);
            SetupCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanSave() => !IsSaving;
}
