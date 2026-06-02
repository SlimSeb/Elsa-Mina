using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.TwentyFortyEight;

public class TwentyFortyEightGame : Game, ITwentyFortyEightGame
{
    private static int NextGameId { get; set; } = 1;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IBotDbContextFactory _dbContextFactory;

    private readonly int _gameId;
    private readonly PeriodicTimerRunner _inactivityTimer;
    private readonly SemaphoreSlim _moveLock = new(1, 1);

    public TwentyFortyEightGame(
        IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IBotDbContextFactory dbContextFactory)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _inactivityTimer = new PeriodicTimerRunner(TwentyFortyEightConstants.INACTIVITY_TIMEOUT,
            OnInactivityTimeout, runOnce: true);
        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(TwentyFortyEightGame);

    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public int Wins { get; private set; }
    public int[,] Grid { get; private set; }
    public bool IsRoundActive { get; private set; }
    public bool IsPrivateMode { get; set; }
    public string TargetRoomId { get; set; }
    public string TargetUserId { get; set; }
    public IContext Context { get; set; }
    public IUser Owner { get; set; }

    private string EffectiveRoomId => IsPrivateMode ? TargetRoomId : Context.RoomId;
    private string GameIdentifier => $"tfe-{EffectiveRoomId}-{_gameId}";

    public async Task DisplayAnnounce()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/TwentyFortyEight/TwentyFortyEightAnnounce",
            new TwentyFortyEightModel
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
        await _moveLock.WaitAsync();
        try
        {
            var savedData = await LoadPlayerDataAsync();
            BestScore = savedData?.BestScore ?? 0;
            Wins = savedData?.Wins ?? 0;

            Score = 0;
            Grid = new int[TwentyFortyEightConstants.GRID_SIZE, TwentyFortyEightConstants.GRID_SIZE];
            SpawnNewTile();
            SpawnNewTile();

            IsRoundActive = true;
            if (!IsStarted)
            {
                OnStart();
            }

            _inactivityTimer.Restart();
            Context.ReplyLocalizedMessage("tfe_game_started");
            await DisplayBoard(firstTime: true);
        }
        finally
        {
            _moveLock.Release();
        }
    }

    public async Task MakeMove(IUser user, string direction)
    {
        await _moveLock.WaitAsync();
        try
        {
            if (!IsRoundActive || user.UserId != Owner.UserId)
            {
                return;
            }

            var moved = direction.ToLowerInvariant() switch
            {
                "up" or "u" => SlideUp(),
                "down" or "d" => SlideDown(),
                "left" or "l" => SlideLeft(),
                "right" or "r" => SlideRight(),
                _ => false
            };

            if (!moved)
            {
                return;
            }

            SpawnNewTile();
            _inactivityTimer.Restart();

            if (GetMaxTile() >= TwentyFortyEightConstants.TARGET_TILE)
            {
                IsRoundActive = false;
                Wins++;
                if (Score > BestScore) BestScore = Score;
                Context.ReplyLocalizedMessage("tfe_game_win", Owner.Name, Score);
                await SavePlayerDataAsync();
                _inactivityTimer.Stop();
                OnEnd();
            }
            else if (!HasMoves())
            {
                IsRoundActive = false;
                if (Score > BestScore) BestScore = Score;
                Context.ReplyLocalizedMessage("tfe_game_lose", Owner.Name, Score);
                await SavePlayerDataAsync();
                _inactivityTimer.Stop();
                OnEnd();
            }

            await DisplayBoard(firstTime: false);
        }
        finally
        {
            _moveLock.Release();
        }
    }

    public async Task CancelAsync()
    {
        await _moveLock.WaitAsync();
        try
        {
            IsRoundActive = false;
            _inactivityTimer.Stop();
            OnEnd();
            await DisplayBoard(firstTime: false);
        }
        finally
        {
            _moveLock.Release();
        }
    }

    private int GetMaxTile()
    {
        var max = 0;
        for (var row = 0; row < TwentyFortyEightConstants.GRID_SIZE; row++)
        {
            for (var col = 0; col < TwentyFortyEightConstants.GRID_SIZE; col++)
            {
                if (Grid[row, col] > max) max = Grid[row, col];
            }
        }

        return max;
    }

    private bool HasMoves()
    {
        var size = TwentyFortyEightConstants.GRID_SIZE;
        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                if (Grid[row, col] == 0) return true;
                if (row + 1 < size && Grid[row, col] == Grid[row + 1, col]) return true;
                if (col + 1 < size && Grid[row, col] == Grid[row, col + 1]) return true;
            }
        }

        return false;
    }

    private void SpawnNewTile()
    {
        var emptyCells = new List<(int Row, int Col)>();
        var size = TwentyFortyEightConstants.GRID_SIZE;
        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                if (Grid[row, col] == 0) emptyCells.Add((row, col));
            }
        }

        if (emptyCells.Count == 0) return;

        var (r, c) = emptyCells[_randomService.NextInt(emptyCells.Count)];
        Grid[r, c] = _randomService.NextInt(10) == 0 ? 4 : 2;
    }

    private bool SlideLeft()
    {
        var changed = false;
        var size = TwentyFortyEightConstants.GRID_SIZE;
        for (var row = 0; row < size; row++)
        {
            var line = GetRow(row);
            var merged = MergeLine(line);
            if (!LinesEqual(line, merged)) changed = true;
            SetRow(row, merged);
        }

        return changed;
    }

    private bool SlideRight()
    {
        var changed = false;
        var size = TwentyFortyEightConstants.GRID_SIZE;
        for (var row = 0; row < size; row++)
        {
            var line = GetRow(row);
            Array.Reverse(line);
            var merged = MergeLine(line);
            if (!LinesEqual(line, merged)) changed = true;
            Array.Reverse(merged);
            SetRow(row, merged);
        }

        return changed;
    }

    private bool SlideUp()
    {
        var changed = false;
        var size = TwentyFortyEightConstants.GRID_SIZE;
        for (var col = 0; col < size; col++)
        {
            var line = GetCol(col);
            var merged = MergeLine(line);
            if (!LinesEqual(line, merged)) changed = true;
            SetCol(col, merged);
        }

        return changed;
    }

    private bool SlideDown()
    {
        var changed = false;
        var size = TwentyFortyEightConstants.GRID_SIZE;
        for (var col = 0; col < size; col++)
        {
            var line = GetCol(col);
            Array.Reverse(line);
            var merged = MergeLine(line);
            if (!LinesEqual(line, merged)) changed = true;
            Array.Reverse(merged);
            SetCol(col, merged);
        }

        return changed;
    }

    private int[] MergeLine(int[] line)
    {
        var filtered = line.Where(value => value != 0).ToArray();
        var result = new int[line.Length];
        var resultIndex = 0;
        var i = 0;
        while (i < filtered.Length)
        {
            if (i + 1 < filtered.Length && filtered[i] == filtered[i + 1])
            {
                var merged = filtered[i] * 2;
                result[resultIndex++] = merged;
                Score += merged;
                i += 2;
            }
            else
            {
                result[resultIndex++] = filtered[i++];
            }
        }

        return result;
    }

    private static bool LinesEqual(int[] a, int[] b)
    {
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }

        return true;
    }

    private int[] GetRow(int row)
    {
        var size = TwentyFortyEightConstants.GRID_SIZE;
        var result = new int[size];
        for (var col = 0; col < size; col++) result[col] = Grid[row, col];
        return result;
    }

    private void SetRow(int row, int[] values)
    {
        for (var col = 0; col < TwentyFortyEightConstants.GRID_SIZE; col++) Grid[row, col] = values[col];
    }

    private int[] GetCol(int col)
    {
        var size = TwentyFortyEightConstants.GRID_SIZE;
        var result = new int[size];
        for (var row = 0; row < size; row++) result[row] = Grid[row, col];
        return result;
    }

    private void SetCol(int col, int[] values)
    {
        for (var row = 0; row < TwentyFortyEightConstants.GRID_SIZE; row++) Grid[row, col] = values[row];
    }

    private async Task OnInactivityTimeout()
    {
        if (IsEnded) return;
        Context.ReplyLocalizedMessage("tfe_game_timeout");
        await CancelAsync();
    }

    private async Task<TwentyFortyEightScore> LoadPlayerDataAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.TwentyFortyEightScores.FindAsync(Owner.UserId);
    }

    private async Task SavePlayerDataAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var record = await db.TwentyFortyEightScores.FindAsync(Owner.UserId);
        if (record == null)
        {
            await db.EnsureUserExistsAsync(Owner.UserId);
            await db.TwentyFortyEightScores.AddAsync(new TwentyFortyEightScore
            {
                UserId = Owner.UserId,
                Wins = Wins,
                BestScore = BestScore
            });
        }
        else
        {
            record.Wins = Wins;
            if (BestScore > record.BestScore) record.BestScore = BestScore;
        }

        await db.SaveChangesAsync();
    }

    private async Task DisplayBoard(bool firstTime)
    {
        var template = await _templatesManager.GetTemplateAsync("Games/TwentyFortyEight/TwentyFortyEightBoard",
            new TwentyFortyEightModel
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
                template.RemoveNewlines(), !firstTime);
        }
        else
        {
            Context.SendUpdatableHtml(GameIdentifier, template.RemoveNewlines(), !firstTime);
        }
    }
}
