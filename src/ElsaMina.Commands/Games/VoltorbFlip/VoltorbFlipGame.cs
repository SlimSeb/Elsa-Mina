using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.VoltorbFlip;

public class VoltorbFlipGame : Game, IVoltorbFlipGame
{
    private static int NextGameId { get; set; } = 1;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IBotDbContextFactory _dbContextFactory;

    private int _revealedCount;
    private int _revealedTwos;
    private int _revealedThrees;
    private int _targetTwos;
    private int _targetThrees;
    private readonly int _gameId;
    private readonly PeriodicTimerRunner _inactivityTimer;

    public VoltorbFlipGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IBotDbContextFactory dbContextFactory)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _inactivityTimer =
            new PeriodicTimerRunner(VoltorbFlipConstants.INACTIVITY_TIMEOUT, OnInactivityTimeout, runOnce: true);

        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(VoltorbFlipGame);

    public int Level { get; private set; } = 1;
    public bool IsRoundActive { get; private set; }
    public int CurrentCoins { get; private set; }
    public int TotalCoins { get; private set; }
    public int[,] TileValues { get; private set; }
    public bool[,] IsRevealed { get; private set; }
    public int[] RowSums { get; private set; }
    public int[] ColSums { get; private set; }
    public int[] RowVoltorbs { get; private set; }
    public int[] ColVoltorbs { get; private set; }
    public bool IsPrivateMode { get; set; }
    public string TargetRoomId { get; set; }
    public string TargetUserId { get; set; }
    public IContext Context { get; set; }
    public IUser Owner { get; set; }
    public bool IsMarkingMode => ActiveMarkerType != VoltorbFlipMarkerType.None;
    public VoltorbFlipMarkerType ActiveMarkerType { get; private set; }
    public VoltorbFlipMarkerType[,] Markers { get; private set; }
    private string EffectiveRoomId => IsPrivateMode ? TargetRoomId : Context.RoomId;
    private string GameIdentifier => $"vf-{EffectiveRoomId}-{_gameId}";
    private string AnnounceId => GameIdentifier;

    public async Task DisplayAnnounce()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/VoltorbFlip/VoltorbFlipAnnounce",
            new VoltorbFlipModel
            {
                Culture = Context.Culture,
                CurrentGame = this,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = EffectiveRoomId,
                IsPrivateMode = IsPrivateMode
            });

        _inactivityTimer.Restart();

        if (IsPrivateMode)
        {
            Context.SendPrivateUpdatableHtml(TargetUserId, TargetRoomId, AnnounceId, template.RemoveNewlines(), false);
        }
        else
        {
            Context.SendUpdatableHtml(AnnounceId, template.RemoveNewlines(), false);
        }
    }

    public async Task StartNewRound()
    {
        var savedData = await LoadPlayerDataAsync();
        Level = savedData?.Level ?? 1;
        TotalCoins = savedData?.Coins ?? 0;

        const int size = VoltorbFlipConstants.GRID_SIZE;
        TileValues = new int[size, size];
        IsRevealed = new bool[size, size];
        Markers = new VoltorbFlipMarkerType[size, size];
        ActiveMarkerType = VoltorbFlipMarkerType.None;
        RowSums = new int[size];
        ColSums = new int[size];
        RowVoltorbs = new int[size];
        ColVoltorbs = new int[size];
        _revealedCount = 0;
        _revealedTwos = 0;
        _revealedThrees = 0;
        CurrentCoins = 0;

        var configs = VoltorbFlipConstants.LEVEL_CONFIGURATIONS[Level - 1];
        var config = _randomService.RandomElement(configs);
        _targetTwos = config.Twos;
        _targetThrees = config.Threes;

        var tiles = new List<int>(size * size);
        for (var i = 0; i < config.Voltorbs; i++)
        {
            tiles.Add(0);
        }

        for (var i = 0; i < config.Threes; i++)
        {
            tiles.Add(3);
        }

        for (var i = 0; i < config.Twos; i++)
        {
            tiles.Add(2);
        }

        while (tiles.Count < size * size)
        {
            tiles.Add(1);
        }

        _randomService.ShuffleInPlace(tiles);

        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                TileValues[row, col] = tiles[row * size + col];
            }
        }

        ComputeRowColStats();
        IsRoundActive = true;

        if (!IsStarted)
        {
            OnStart();
        }

        _inactivityTimer.Restart();

        Context.ReplyLocalizedMessage("vf_game_started", Level);
        await DisplayBoard(showAll: false, firstTime: true);
    }

    public async Task FlipTile(IUser user, int row, int col)
    {
        if (!IsRoundActive || user.UserId != Owner.UserId)
        {
            return;
        }

        const int size = VoltorbFlipConstants.GRID_SIZE;
        if (row < 0 || row >= size || col < 0 || col >= size)
        {
            return;
        }

        if (IsRevealed[row, col])
        {
            return;
        }

        if (IsMarkingMode)
        {
            Markers[row, col] = Markers[row, col] == ActiveMarkerType
                ? VoltorbFlipMarkerType.None
                : ActiveMarkerType;
            _inactivityTimer.Restart();
            await DisplayBoard(showAll: false, firstTime: false);
            return;
        }

        IsRevealed[row, col] = true;
        var value = TileValues[row, col];

        if (value == 0)
        {
            IsRoundActive = false;
            Level = ComputeNewLevelOnLossOrQuit();
            Context.ReplyLocalizedMessage("vf_game_voltorb_hit", user.Name, Level);
            await SavePlayerDataAsync();
            _inactivityTimer.Stop();
            OnEnd();
            await DisplayBoard(showAll: true, firstTime: false);
            return;
        }

        _revealedCount++;
        if (value == 2)
        {
            _revealedTwos++;
        }
        else if (value == 3)
        {
            _revealedThrees++;
        }

        UpdateCoins();

        if (_revealedTwos >= _targetTwos && _revealedThrees >= _targetThrees)
        {
            IsRoundActive = false;
            var earnedCoins = CurrentCoins;
            Level = Math.Min(Level + 1, VoltorbFlipConstants.MAX_LEVEL);
            Context.ReplyLocalizedMessage("vf_game_win", user.Name, earnedCoins, Level);
            await SavePlayerDataAsync(earnedCoins);
            _inactivityTimer.Stop();
            OnEnd();
            await DisplayBoard(showAll: true, firstTime: true);
            return;
        }

        _inactivityTimer.Restart();
        await DisplayBoard(showAll: false, firstTime: true);
    }

    public async Task QuitRound(IUser user)
    {
        if (!IsRoundActive || user.UserId != Owner.UserId)
        {
            return;
        }

        IsRoundActive = false;
        var earnedCoins = CurrentCoins;
        Level = ComputeNewLevelOnLossOrQuit();
        Context.ReplyLocalizedMessage("vf_game_quit", user.Name, earnedCoins, Level);
        await SavePlayerDataAsync(earnedCoins);
        _inactivityTimer.Stop();
        OnEnd();
        await DisplayBoard(showAll: true, firstTime: true);
    }

    public async Task SetMarkerType(IUser user, VoltorbFlipMarkerType markerType)
    {
        if (!IsRoundActive || user.UserId != Owner.UserId)
        {
            return;
        }

        ActiveMarkerType = ActiveMarkerType == markerType
            ? VoltorbFlipMarkerType.None
            : markerType;
        _inactivityTimer.Restart();
        await DisplayBoard(showAll: false, firstTime: false);
    }

    public async Task CancelAsync()
    {
        IsRoundActive = false;
        _inactivityTimer.Stop();
        if (Owner != null)
        {
            try
            {
                await SavePlayerDataAsync();
            }
            catch
            {
                // ignore DB errors on cancel
            }
        }

        OnEnd();
        await DisplayBoard(showAll: true, firstTime: false);
    }

    private async Task OnInactivityTimeout()
    {
        if (IsEnded)
        {
            return;
        }

        Context.ReplyLocalizedMessage("vf_game_timeout");
        await CancelAsync();
        await DisplayBoard(showAll: true, firstTime: false);
    }

    private async Task<VoltorbFlipLevel> LoadPlayerDataAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.VoltorbFlipLevels.FindAsync(Owner.UserId);
    }

    private async Task SavePlayerDataAsync(int coinsEarned = 0)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var record = await db.VoltorbFlipLevels.FindAsync(Owner.UserId);
        if (record == null)
        {
            TotalCoins = coinsEarned;
            await db.EnsureUserExistsAsync(Owner.UserId);
            await db.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel
            {
                UserId = Owner.UserId,
                Level = Level,
                MaxLevel = Level,
                Coins = coinsEarned
            });
        }
        else
        {
            record.Level = Level;
            record.MaxLevel = Math.Max(record.MaxLevel, Level);
            record.Coins += coinsEarned;
            TotalCoins = record.Coins;
        }

        await db.SaveChangesAsync();
    }

    private void ComputeRowColStats()
    {
        const int size = VoltorbFlipConstants.GRID_SIZE;
        for (var row = 0; row < size; row++)
        {
            RowSums[row] = 0;
            RowVoltorbs[row] = 0;
            for (var col = 0; col < size; col++)
            {
                var value = TileValues[row, col];
                if (value == 0)
                {
                    RowVoltorbs[row]++;
                }
                else
                {
                    RowSums[row] += value;
                }
            }
        }

        for (var col = 0; col < size; col++)
        {
            ColSums[col] = 0;
            ColVoltorbs[col] = 0;
            for (var row = 0; row < size; row++)
            {
                var value = TileValues[row, col];
                if (value == 0)
                {
                    ColVoltorbs[col]++;
                }
                else
                {
                    ColSums[col] += value;
                }
            }
        }
    }

    private void UpdateCoins()
    {
        CurrentCoins = _revealedCount > 0
            ? (int)(Math.Pow(2, _revealedTwos) * Math.Pow(3, _revealedThrees))
            : 0;
    }

    private int ComputeNewLevelOnLossOrQuit()
    {
        return _revealedCount < Level
            ? Math.Max(1, _revealedCount)
            : Level;
    }

    private async Task DisplayBoard(bool showAll, bool firstTime)
    {
        var template = await _templatesManager.GetTemplateAsync("Games/VoltorbFlip/VoltorbFlipBoard",
            new VoltorbFlipModel
            {
                Culture = Context.Culture,
                CurrentGame = this,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = EffectiveRoomId,
                ShowAll = showAll,
                IsPrivateMode = IsPrivateMode
            });

        if (IsPrivateMode)
        {
            Context.SendPrivateUpdatableHtml(TargetUserId, TargetRoomId, GameIdentifier,
                template.RemoveNewlines().CollapseAttributeWhitespace(), !firstTime);
        }
        else
        {
            Context.SendUpdatableHtml(GameIdentifier, template.RemoveNewlines().CollapseAttributeWhitespace(),
                !firstTime);
        }
    }
}