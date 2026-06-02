using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.FloodIt;

public class FloodItGame : Game, IFloodItGame
{
    private static int NextGameId { get; set; } = 1;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IBotDbContextFactory _dbContextFactory;

    private readonly int _gameId;
    private readonly PeriodicTimerRunner _inactivityTimer;
    private int _targetMoves;
    private int _claimedCount;

    public FloodItGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IBotDbContextFactory dbContextFactory)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _inactivityTimer = new PeriodicTimerRunner(FloodItConstants.INACTIVITY_TIMEOUT, OnInactivityTimeout,
            runOnce: true);

        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(FloodItGame);

    public int Level { get; private set; } = 1;
    public int GridSize { get; private set; } = 8;
    public int ColorCount { get; private set; } = 3;
    public int MoveCount { get; private set; }
    public int MaxMoves { get; private set; }
    public int Stars { get; private set; }
    public int TotalStars { get; private set; }
    public bool IsRoundActive { get; private set; }
    public int[,] Grid { get; private set; }
    public bool[,] IsClaimed { get; private set; }
    public bool IsPrivateMode { get; set; }
    public string TargetRoomId { get; set; }
    public string TargetUserId { get; set; }
    public IContext Context { get; set; }
    public IUser Owner { get; set; }

    private string EffectiveRoomId => IsPrivateMode ? TargetRoomId : Context.RoomId;
    private string GameIdentifier => $"fi-{EffectiveRoomId}-{_gameId}";

    public async Task DisplayAnnounce()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/FloodIt/FloodItAnnounce",
            new FloodItModel
            {
                Culture = Context.Culture,
                CurrentGame = this,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = EffectiveRoomId,
                IsPrivateMode = IsPrivateMode
            });

        _inactivityTimer.Restart();
        Context.SendUpdatableHtml(GameIdentifier, template.RemoveNewlines(), false);
    }

    public async Task StartNewRound()
    {
        var savedData = await LoadPlayerDataAsync();
        Level = savedData?.Level ?? 1;
        TotalStars = savedData?.TotalStars ?? 0;

        var config = FloodItConstants.GetLevelConfig(Level);
        GridSize = config.GridSize;
        ColorCount = config.ColorCount;
        _targetMoves = config.TargetMoves;
        MaxMoves = config.MaxMoves;

        MoveCount = 0;
        Stars = 0;

        do
        {
            Grid = new int[GridSize, GridSize];
            IsClaimed = new bool[GridSize, GridSize];
            _claimedCount = 0;
            GenerateRandomGrid();
            InitializeClaimedRegion();
        } while (ComputeGreedySolutionLength() > MaxMoves);

        IsRoundActive = true;
        if (!IsStarted)
        {
            OnStart();
        }

        _inactivityTimer.Restart();
        Context.ReplyLocalizedMessage("fi_game_started", Level, GridSize, GridSize);
        await DisplayBoard(firstTime: true);
    }

    public async Task FloodFill(IUser user, int colorIndex)
    {
        if (!IsRoundActive || user.UserId != Owner.UserId)
        {
            return;
        }

        if (colorIndex < 0 || colorIndex >= ColorCount)
        {
            return;
        }

        var currentColor = Grid[0, 0];
        if (colorIndex == currentColor)
        {
            return;
        }

        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                if (IsClaimed[row, col])
                {
                    Grid[row, col] = colorIndex;
                }
            }
        }

        var queue = new Queue<(int Row, int Col)>();
        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                if (!IsClaimed[row, col]) continue;
                foreach (var (nr, nc) in GetNeighbors(row, col))
                {
                    if (!IsClaimed[nr, nc] && Grid[nr, nc] == colorIndex)
                    {
                        IsClaimed[nr, nc] = true;
                        _claimedCount++;
                        queue.Enqueue((nr, nc));
                    }
                }
            }
        }

        while (queue.Count > 0)
        {
            var (row, col) = queue.Dequeue();
            foreach (var (nr, nc) in GetNeighbors(row, col))
            {
                if (!IsClaimed[nr, nc] && Grid[nr, nc] == colorIndex)
                {
                    IsClaimed[nr, nc] = true;
                    _claimedCount++;
                    queue.Enqueue((nr, nc));
                }
            }
        }

        MoveCount++;
        _inactivityTimer.Restart();

        if (_claimedCount == GridSize * GridSize)
        {
            IsRoundActive = false;
            Stars = ComputeStars();
            TotalStars += Stars;
            Level = Math.Min(Level + 1, FloodItConstants.MAX_LEVEL);
            Context.ReplyLocalizedMessage("fi_game_win", Owner.Name, MoveCount, Stars, Level);
            await SavePlayerDataAsync(Stars);
            _inactivityTimer.Stop();
            OnEnd();
        }
        else if (MoveCount >= MaxMoves)
        {
            IsRoundActive = false;
            Level = Math.Max(1, Level - 1);
            Context.ReplyLocalizedMessage("fi_game_lose", Owner.Name, MaxMoves);
            await SaveLevelOnlyAsync();
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

    private void GenerateRandomGrid()
    {
        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                Grid[row, col] = _randomService.NextInt(ColorCount);
            }
        }
    }

    private void InitializeClaimedRegion()
    {
        var startColor = Grid[0, 0];
        var queue = new Queue<(int Row, int Col)>();
        queue.Enqueue((0, 0));
        IsClaimed[0, 0] = true;
        _claimedCount = 1;

        while (queue.Count > 0)
        {
            var (row, col) = queue.Dequeue();
            foreach (var (nr, nc) in GetNeighbors(row, col))
            {
                if (!IsClaimed[nr, nc] && Grid[nr, nc] == startColor)
                {
                    IsClaimed[nr, nc] = true;
                    _claimedCount++;
                    queue.Enqueue((nr, nc));
                }
            }
        }
    }

    private IEnumerable<(int Row, int Col)> GetNeighbors(int row, int col)
    {
        if (row > 0) yield return (row - 1, col);
        if (row < GridSize - 1) yield return (row + 1, col);
        if (col > 0) yield return (row, col - 1);
        if (col < GridSize - 1) yield return (row, col + 1);
    }

    private int ComputeStars()
    {
        if (MoveCount <= _targetMoves) return 3;
        if (MoveCount <= (_targetMoves + MaxMoves) / 2) return 2;
        return 1;
    }

    private int ComputeGreedySolutionLength()
    {
        var grid = (int[,])Grid.Clone();
        var claimed = (bool[,])IsClaimed.Clone();
        var claimedCount = _claimedCount;
        var moves = 0;

        while (claimedCount < GridSize * GridSize)
        {
            var bestColor = -1;
            var bestGain = 0;
            var currentColor = grid[0, 0];

            for (var color = 0; color < ColorCount; color++)
            {
                if (color == currentColor) continue;
                var gain = CountGainForColor(grid, claimed, color);
                if (gain > bestGain)
                {
                    bestGain = gain;
                    bestColor = color;
                }
            }

            if (bestColor == -1)
            {
                break;
            }

            ApplyFloodFillToArrays(grid, claimed, ref claimedCount, bestColor);
            moves++;

            if (moves > MaxMoves)
            {
                return moves;
            }
        }

        return moves;
    }

    private int CountGainForColor(int[,] grid, bool[,] claimed, int colorIndex)
    {
        var queued = new bool[GridSize, GridSize];
        var queue = new Queue<(int Row, int Col)>();

        for (var r = 0; r < GridSize; r++)
        {
            for (var c = 0; c < GridSize; c++)
            {
                if (!claimed[r, c]) continue;
                foreach (var (nr, nc) in GetNeighbors(r, c))
                {
                    if (!claimed[nr, nc] && grid[nr, nc] == colorIndex && !queued[nr, nc])
                    {
                        queued[nr, nc] = true;
                        queue.Enqueue((nr, nc));
                    }
                }
            }
        }

        var gain = 0;
        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            gain++;
            foreach (var (nr, nc) in GetNeighbors(r, c))
            {
                if (!claimed[nr, nc] && !queued[nr, nc] && grid[nr, nc] == colorIndex)
                {
                    queued[nr, nc] = true;
                    queue.Enqueue((nr, nc));
                }
            }
        }

        return gain;
    }

    private void ApplyFloodFillToArrays(int[,] grid, bool[,] claimed, ref int claimedCount, int colorIndex)
    {
        for (var r = 0; r < GridSize; r++)
        {
            for (var c = 0; c < GridSize; c++)
            {
                if (claimed[r, c]) grid[r, c] = colorIndex;
            }
        }

        var queue = new Queue<(int Row, int Col)>();
        for (var r = 0; r < GridSize; r++)
        {
            for (var c = 0; c < GridSize; c++)
            {
                if (!claimed[r, c]) continue;
                foreach (var (nr, nc) in GetNeighbors(r, c))
                {
                    if (!claimed[nr, nc] && grid[nr, nc] == colorIndex)
                    {
                        claimed[nr, nc] = true;
                        claimedCount++;
                        queue.Enqueue((nr, nc));
                    }
                }
            }
        }

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            foreach (var (nr, nc) in GetNeighbors(r, c))
            {
                if (!claimed[nr, nc] && grid[nr, nc] == colorIndex)
                {
                    claimed[nr, nc] = true;
                    claimedCount++;
                    queue.Enqueue((nr, nc));
                }
            }
        }
    }

    private async Task OnInactivityTimeout()
    {
        if (IsEnded)
        {
            return;
        }

        Context.ReplyLocalizedMessage("fi_game_timeout");
        await CancelAsync();
    }

    private async Task<FloodItScore> LoadPlayerDataAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.FloodItScores.FindAsync(Owner.UserId);
    }

    private async Task SavePlayerDataAsync(int starsEarned)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var record = await db.FloodItScores.FindAsync(Owner.UserId);
        if (record == null)
        {
            await db.EnsureUserExistsAsync(Owner.UserId);
            await db.FloodItScores.AddAsync(new FloodItScore
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
            var record = await db.FloodItScores.FindAsync(Owner.UserId);
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
        var template = await _templatesManager.GetTemplateAsync("Games/FloodIt/FloodItBoard",
            new FloodItModel
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
            Context.SendPrivateUpdatableHtml(TargetUserId, TargetRoomId, GameIdentifier,
                template.RemoveNewlines().CollapseAttributeWhitespace().RemoveWhitespacesBetweenTags(),
                !firstTime);
        }
        else
        {
            Context.SendUpdatableHtml(GameIdentifier,
                template.RemoveNewlines().CollapseAttributeWhitespace().RemoveWhitespacesBetweenTags(),
                !firstTime);
        }
    }
}