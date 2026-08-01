using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.PlayTime;
using ElsaMina.Core.Services.Repeats;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Core.Services.Start;

public class StartManager : IStartManager
{
    private readonly ITemplatesManager _templatesManager;
    private readonly ICustomColorsManager _customColorsManager;
    private readonly IRoomColorsCache _roomColorsCache;
    private readonly IRoomUserDataService _roomUserDataService;
    private readonly IDexManager _dexManager;
    private readonly IPlayTimeUpdateService _playTimeUpdateService;
    private readonly IRepeatsManager _repeatsManager;

    public StartManager(ITemplatesManager templatesManager,
        ICustomColorsManager customColorsManager,
        IRoomColorsCache roomColorsCache,
        IRoomUserDataService roomUserDataService,
        IDexManager dexManager,
        IPlayTimeUpdateService playTimeUpdateService,
        IRepeatsManager repeatsManager)
    {
        _templatesManager = templatesManager;
        _customColorsManager = customColorsManager;
        _roomColorsCache = roomColorsCache;
        _roomUserDataService = roomUserDataService;
        _dexManager = dexManager;
        _playTimeUpdateService = playTimeUpdateService;
        _repeatsManager = repeatsManager;
    }

    public async Task LoadStaticDataAsync(CancellationToken cancellationToken = default)
    {
        _playTimeUpdateService.Initialize();
        await Task.WhenAll(
            _templatesManager.CompileTemplatesAsync(),
            _customColorsManager.FetchCustomColorsAsync(cancellationToken),
            _roomColorsCache.LoadAsync(cancellationToken),
            _roomUserDataService.InitializeJoinPhrasesAsync(cancellationToken),
            _dexManager.LoadDexAsync(cancellationToken),
            _repeatsManager.InitializeAsync(cancellationToken)
        );
    }
}
