using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.LightsOut;

public class LightsOutGame : Game, ILightsOutGame
{
    private static int NextGameId { get; set; } = 1;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IBotDbContextFactory _dbContextFactory;

    private readonly int _gameId;
    private readonly PeriodicTimerRunner _inactivityTimer;
    private int _presses;

    public LightsOutGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IBotDbContextFactory dbContextFactory)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _inactivityTimer = new PeriodicTimerRunner(LightsOutConstants.INACTIVITY_TIMEOUT, OnInactivityTimeout, runOnce: true);

        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(LightsOutGame);

    public int Level { get; private set; } = 1;
    public int GridSize { get; private set; } = 5;
    public int MoveCount { get; private set; }
    public int Stars { get; private set; }
    public int TotalStars { get; private set; }
    public bool IsRoundActive { get; private set; }
    public bool[,] Grid { get; private set; }
    public bool IsPrivateMode { get; set; }
    public string TargetRoomId { get; set; }
    public string TargetUserId { get; set; }
    public IContext Context { get; set; }
    public IUser Owner { get; set; }

    private string EffectiveRoomId => IsPrivateMode ? TargetRoomId : Context.RoomId;
    private string GameIdentifier => $"lo-{EffectiveRoomId}-{_gameId}";
    private string AnnounceId => GameIdentifier;

    public async Task DisplayAnnounce()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/LightsOut/LightsOutAnnounce",
            new LightsOutModel
            {
                Culture = Context.Culture,
                CurrentGame = this,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = EffectiveRoomId,
                IsPrivateMode = IsPrivateMode
            });

        _inactivityTimer.Restart();
        Context.SendUpdatableHtml(AnnounceId, template.RemoveNewlines(), false);
    }

    public async Task StartNewRound()
    {
        var savedData = await LoadPlayerDataAsync();
        Level = savedData?.Level ?? 1;
        TotalStars = savedData?.TotalStars ?? 0;

        var config = LightsOutConstants.GetLevelConfig(Level);
        GridSize = config.GridSize;
        _presses = config.Presses;

        Grid = new bool[GridSize, GridSize];
        MoveCount = 0;
        Stars = 0;

        GenerateSolvablePuzzle();

        IsRoundActive = true;
        if (!IsStarted)
        {
            OnStart();
        }

        _inactivityTimer.Restart();
        Context.ReplyLocalizedMessage("lo_game_started", Level, GridSize, GridSize);
        await DisplayBoard(firstTime: true);
    }

    public async Task ToggleCell(IUser user, int row, int col)
    {
        if (!IsRoundActive || user.UserId != Owner.UserId)
        {
            return;
        }

        if (row < 0 || row >= GridSize || col < 0 || col >= GridSize)
        {
            return;
        }

        ToggleWithNeighbors(row, col);
        MoveCount++;
        _inactivityTimer.Restart();

        if (IsSolved())
        {
            IsRoundActive = false;
            Stars = ComputeStars();
            TotalStars += Stars;
            Level = Math.Min(Level + 1, LightsOutConstants.MAX_LEVEL);
            Context.ReplyLocalizedMessage("lo_game_win", Owner.Name, MoveCount, Stars, Level);
            await SavePlayerDataAsync(Stars);
            _inactivityTimer.Stop();
            OnEnd();
        }

        await DisplayBoard(firstTime: false);
    }

    public async Task CancelAsync()
    {
        if (IsRoundActive)
        {
            Level = Math.Max(1, Level - 1);
            await SaveLevelOnlyAsync();
        }
        IsRoundActive = false;
        _inactivityTimer.Stop();
        OnEnd();
        await DisplayBoard(firstTime: false);
    }

    private void ToggleWithNeighbors(int row, int col)
    {
        Toggle(row, col);
        if (row > 0) Toggle(row - 1, col);
        if (row < GridSize - 1) Toggle(row + 1, col);
        if (col > 0) Toggle(row, col - 1);
        if (col < GridSize - 1) Toggle(row, col + 1);
    }

    private void Toggle(int row, int col)
    {
        Grid[row, col] = !Grid[row, col];
    }

    private bool IsSolved()
    {
        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                if (Grid[row, col])
                {
                    return false;
                }
            }
        }
        return true;
    }

    private int ComputeStars()
    {
        if (MoveCount <= _presses) return 3;
        if (MoveCount <= _presses * 2) return 2;
        return 1;
    }

    private void GenerateSolvablePuzzle()
    {
        for (var press = 0; press < _presses; press++)
        {
            var row = _randomService.NextInt(GridSize);
            var col = _randomService.NextInt(GridSize);
            ToggleWithNeighbors(row, col);
        }

        if (IsSolved())
        {
            var row = _randomService.NextInt(GridSize);
            var col = _randomService.NextInt(GridSize);
            ToggleWithNeighbors(row, col);
        }
    }

    private async Task OnInactivityTimeout()
    {
        if (IsEnded)
        {
            return;
        }

        Context.ReplyLocalizedMessage("lo_game_timeout");
        await CancelAsync();
    }

    private async Task<LightsOutScore> LoadPlayerDataAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.LightsOutScores.FindAsync(Owner.UserId);
    }

    private async Task SavePlayerDataAsync(int starsEarned)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var record = await db.LightsOutScores.FindAsync(Owner.UserId);
        if (record == null)
        {
            await db.EnsureUserExistsAsync(Owner.UserId);
            await db.LightsOutScores.AddAsync(new LightsOutScore
            {
                UserId = Owner.UserId,
                Level = Level,
                BestMoves = MoveCount,
                TotalStars = starsEarned
            });
        }
        else
        {
            record.Level = Math.Max(record.Level, Level);
            record.TotalStars += starsEarned;
            if (record.BestMoves == 0 || MoveCount < record.BestMoves)
            {
                record.BestMoves = MoveCount;
            }
        }
        await db.SaveChangesAsync();
    }

    private async Task SaveLevelOnlyAsync()
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var record = await db.LightsOutScores.FindAsync(Owner.UserId);
            if (record != null)
            {
                record.Level = Level;
                await db.SaveChangesAsync();
            }
        }
        catch
        {
            // ignore DB errors on cancel
        }
    }

    private async Task DisplayBoard(bool firstTime)
    {
        var template = await _templatesManager.GetTemplateAsync("Games/LightsOut/LightsOutBoard",
            new LightsOutModel
            {
                Culture = Context.Culture,
                CurrentGame = this,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = EffectiveRoomId,
                IsPrivateMode = IsPrivateMode
            });

        if (IsPrivateMode)
        {
            Context.SendPrivateUpdatableHtml(TargetUserId, TargetRoomId, GameIdentifier, template.RemoveNewlines(), !firstTime);
        }
        else
        {
            Context.SendUpdatableHtml(GameIdentifier, template.RemoveNewlines(), !firstTime);
        }
    }
}
