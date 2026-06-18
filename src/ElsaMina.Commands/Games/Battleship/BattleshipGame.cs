using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.Battleship;

public class BattleshipGame : Game, IBattleshipGame
{
    private static int NextGameId { get; set; } = 1;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IBattleshipRatingService _ratingService;

    private readonly PeriodicTimerRunner _turnTimeoutTimer;
    private readonly PeriodicTimerRunner _placementTimeoutTimer;
    private readonly SemaphoreSlim _joinSemaphore = new(1, 1);
    private readonly List<BattleshipPlayer> _players = [];
    private readonly List<string> _log = [];
    private BattleshipPhase _phase = BattleshipPhase.Joining;
    private bool _ended;

    [UsedImplicitly]
    public BattleshipGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IBattleshipRatingService ratingService) : this(randomService, templatesManager, configuration,
        ratingService, BattleshipConstants.TIMEOUT_DELAY)
    {
    }

    public BattleshipGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IBattleshipRatingService ratingService,
        TimeSpan timeoutDelay) : this(randomService, templatesManager, configuration, ratingService, timeoutDelay,
        BattleshipConstants.PLACEMENT_TIMEOUT_DELAY)
    {
    }

    public BattleshipGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IBattleshipRatingService ratingService,
        TimeSpan timeoutDelay,
        TimeSpan placementTimeoutDelay)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _ratingService = ratingService;
        _turnTimeoutTimer = new PeriodicTimerRunner(timeoutDelay, OnTimeout, runOnce: true);
        _placementTimeoutTimer = new PeriodicTimerRunner(placementTimeoutDelay, OnPlacementTimeout, runOnce: true);

        GameId = NextGameId++;
    }

    #region Properties

    public IReadOnlyList<BattleshipPlayer> Players => _players;

    public BattleshipPlayer CurrentPlayer { get; private set; }

    public IUser PlayerCurrentlyPlaying => CurrentPlayer?.User;

    public int TurnCount { get; private set; }

    public bool IsPlacementPhase => _phase == BattleshipPhase.Placement;

    public string WinnerName { get; private set; }

    public override string Identifier => nameof(BattleshipGame);

    public string PlayerNames => string.Join(", ", _players.Select(player => player.User.Name));

    public int GameId { get; }

    public IContext Context { get; set; }

    private string AnnounceId => $"battleship-announce-{GameId}";

    private string PlayerPageId => $"battleship-{GameId}";

    #endregion

    #region Public Methods

    public async Task DisplayAnnounce()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/Battleship/BattleshipPanel",
            BuildModel(null));

        Context.SendUpdatableHtml(AnnounceId, template.RemoveNewlines(), false);
    }

    public async Task JoinGame(IUser user)
    {
        await _joinSemaphore.WaitAsync();
        try
        {
            if (_phase != BattleshipPhase.Joining || _players.Count >= BattleshipConstants.MAX_PLAYERS_COUNT)
            {
                return;
            }

            if (_players.Any(player => Equals(player.User, user)))
            {
                return;
            }

            _players.Add(new BattleshipPlayer(user));
            if (_players.Count >= BattleshipConstants.MAX_PLAYERS_COUNT)
            {
                await BeginPlacementPhase();
            }
        }
        finally
        {
            _joinSemaphore.Release();
        }
    }

    public async Task PlaceShip(IUser user, string coordinate)
    {
        if (_phase != BattleshipPhase.Placement)
        {
            return;
        }

        var player = _players.FirstOrDefault(candidate => Equals(candidate.User, user));
        if (player is null || player.HasPlacedAllShips)
        {
            return;
        }

        if (!TryParseCoordinate(coordinate, out var row, out var column))
        {
            return;
        }

        var shipType = BattleshipConstants.FLEET[player.NextShipIndex];
        if (!TryComputeShipCells(player.Board, row, column, shipType.Size, player.IsHorizontalPlacement, out var cells))
        {
            await RenderPlayerPage(player); // Invalid placement: just refresh the board
            return;
        }

        var ship = new BattleshipShip { NameKey = shipType.NameKey, Size = shipType.Size };
        foreach (var cell in cells)
        {
            ship.Cells.Add(cell);
            player.Board.ShipGrid[cell.Row, cell.Column] = ship;
        }

        player.Board.Ships.Add(ship);
        player.NextShipIndex++;

        await OnPlacementProgress(player);
    }

    public async Task ToggleOrientation(IUser user)
    {
        if (_phase != BattleshipPhase.Placement)
        {
            return;
        }

        var player = _players.FirstOrDefault(candidate => Equals(candidate.User, user));
        if (player is null || player.HasPlacedAllShips)
        {
            return;
        }

        player.IsHorizontalPlacement = !player.IsHorizontalPlacement;
        await RenderPlayerPage(player);
    }

    public async Task RandomPlaceRemaining(IUser user)
    {
        if (_phase != BattleshipPhase.Placement)
        {
            return;
        }

        var player = _players.FirstOrDefault(candidate => Equals(candidate.User, user));
        if (player is null || player.HasPlacedAllShips)
        {
            return;
        }

        PlaceShipsRandomly(player.Board, player.NextShipIndex);
        player.NextShipIndex = BattleshipConstants.FLEET.Count;

        await OnPlacementProgress(player);
    }

    public async Task ResetPlacement(IUser user)
    {
        if (_phase != BattleshipPhase.Placement)
        {
            return;
        }

        var player = _players.FirstOrDefault(candidate => Equals(candidate.User, user));
        if (player is null)
        {
            return;
        }

        ClearBoard(player.Board);
        player.NextShipIndex = 0;
        await RenderPlayerPage(player);
    }

    public async Task Fire(IUser user, string coordinate)
    {
        if (_phase != BattleshipPhase.Playing || CurrentPlayer is null || !Equals(user, CurrentPlayer.User))
        {
            return;
        }

        if (!TryParseCoordinate(coordinate, out var row, out var column))
        {
            return;
        }

        var opponent = _players.First(player => !Equals(player.User, user));
        var board = opponent.Board;

        if (board.Shots[row, column] != CellShotState.None)
        {
            return; // Already fired at this cell
        }

        board.LastShot = (row, column);
        var ship = board.ShipGrid[row, column];
        if (ship is null)
        {
            board.Shots[row, column] = CellShotState.Miss;
            AddLog("battleship_shot_miss", user.Name, BattleshipConstants.FormatCoordinate(row, column));
        }
        else
        {
            board.Shots[row, column] = CellShotState.Hit;
            ship.Hits++;
            if (ship.IsSunk)
            {
                AddLog("battleship_shot_sunk", user.Name, opponent.User.Name,
                    Context.GetString($"battleship_ship_{ship.NameKey}"));
            }
            else
            {
                AddLog("battleship_shot_hit", user.Name, BattleshipConstants.FormatCoordinate(row, column));
            }

            if (board.AllShipsSunk)
            {
                await FinishAsync(CurrentPlayer, opponent);
                return;
            }
        }

        await InitializeNextTurn();
    }

    public async Task Forfeit(IUser user)
    {
        if (_phase is not (BattleshipPhase.Placement or BattleshipPhase.Playing))
        {
            return;
        }

        var loser = _players.FirstOrDefault(player => Equals(player.User, user));
        if (loser is null)
        {
            return;
        }

        AddLog("battleship_player_forfeited", user.Name);
        var winner = _players.First(player => !Equals(player.User, user));
        await FinishAsync(winner, loser);
    }

    public async Task OnTimeout()
    {
        if (_phase != BattleshipPhase.Playing || CurrentPlayer is null)
        {
            return;
        }

        AddLog("battleship_on_timeout", CurrentPlayer.User.Name);
        var loser = CurrentPlayer;
        var winner = _players.First(player => !Equals(player.User, loser.User));
        await FinishAsync(winner, loser);
    }

    public async Task OnPlacementTimeout()
    {
        if (_phase != BattleshipPhase.Placement)
        {
            return;
        }

        foreach (var player in _players.Where(player => !player.HasPlacedAllShips))
        {
            PlaceShipsRandomly(player.Board, player.NextShipIndex);
            player.NextShipIndex = BattleshipConstants.FLEET.Count;
        }

        AddLog("battleship_placement_timeout");
        await BeginPlayingPhase();
    }

    public void Cancel()
    {
        _phase = BattleshipPhase.Finished;
        OnEnd();
        _turnTimeoutTimer.Stop();
        _placementTimeoutTimer.Stop();
        Context.SendUpdatableHtml(AnnounceId, string.Empty, true);
        foreach (var player in _players)
        {
            Context.CloseHtmlPage(player.User.UserId, PlayerPageId);
        }
    }

    #endregion

    #region Private Methods

    private async Task BeginPlacementPhase()
    {
        _phase = BattleshipPhase.Placement;
        await RenderPublicPanel();
        await RenderPlayerPages();
        _placementTimeoutTimer.Restart();
    }

    private async Task OnPlacementProgress(BattleshipPlayer player)
    {
        if (_players.All(candidate => candidate.HasPlacedAllShips))
        {
            await BeginPlayingPhase();
            return;
        }

        await RenderPlayerPage(player);
    }

    private async Task BeginPlayingPhase()
    {
        _placementTimeoutTimer.Stop();
        _phase = BattleshipPhase.Playing;

        OnStart();
        _randomService.ShuffleInPlace(_players);
        await InitializeNextTurn();
    }

    private void PlaceShipsRandomly(BattleshipBoard board, int startIndex)
    {
        for (var shipIndex = startIndex; shipIndex < BattleshipConstants.FLEET.Count; shipIndex++)
        {
            var shipType = BattleshipConstants.FLEET[shipIndex];
            var placed = false;
            while (!placed)
            {
                var isHorizontal = _randomService.NextInt(2) == 0;
                var rowUpperBound = isHorizontal
                    ? BattleshipConstants.BOARD_SIZE
                    : BattleshipConstants.BOARD_SIZE - shipType.Size + 1;
                var columnUpperBound = isHorizontal
                    ? BattleshipConstants.BOARD_SIZE - shipType.Size + 1
                    : BattleshipConstants.BOARD_SIZE;

                var startRow = _randomService.NextInt(rowUpperBound);
                var startColumn = _randomService.NextInt(columnUpperBound);

                if (!TryComputeShipCells(board, startRow, startColumn, shipType.Size, isHorizontal, out var cells))
                {
                    continue;
                }

                var ship = new BattleshipShip { NameKey = shipType.NameKey, Size = shipType.Size };
                foreach (var cell in cells)
                {
                    ship.Cells.Add(cell);
                    board.ShipGrid[cell.Row, cell.Column] = ship;
                }

                board.Ships.Add(ship);
                placed = true;
            }
        }
    }

    private static bool TryComputeShipCells(BattleshipBoard board, int startRow, int startColumn, int shipSize,
        bool isHorizontal, out List<(int Row, int Column)> cells)
    {
        cells = [];
        for (var offset = 0; offset < shipSize; offset++)
        {
            var row = isHorizontal ? startRow : startRow + offset;
            var column = isHorizontal ? startColumn + offset : startColumn;

            if (row < 0 || row >= BattleshipConstants.BOARD_SIZE
                        || column < 0 || column >= BattleshipConstants.BOARD_SIZE)
            {
                cells = [];
                return false;
            }

            if (board.ShipGrid[row, column] is not null)
            {
                cells = [];
                return false;
            }

            cells.Add((row, column));
        }

        return true;
    }

    private static void ClearBoard(BattleshipBoard board)
    {
        board.Ships.Clear();
        for (var row = 0; row < BattleshipConstants.BOARD_SIZE; row++)
        {
            for (var column = 0; column < BattleshipConstants.BOARD_SIZE; column++)
            {
                board.ShipGrid[row, column] = null;
            }
        }
    }

    private async Task InitializeNextTurn()
    {
        TurnCount++;
        CurrentPlayer = _players[(TurnCount - 1) % BattleshipConstants.MAX_PLAYERS_COUNT];
        await RenderPublicPanel();
        await RenderPlayerPages();

        _turnTimeoutTimer.Restart();
    }

    private async Task FinishAsync(BattleshipPlayer winner, BattleshipPlayer loser)
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        _phase = BattleshipPhase.Finished;
        WinnerName = winner.User.Name;
        OnEnd();
        _turnTimeoutTimer.Stop();
        _placementTimeoutTimer.Stop();

        AddLog("battleship_win_message", winner.User.Name);
        var (winnerChange, loserChange) = await _ratingService.UpdateRatingsOnWinAsync(winner.User, loser.User);
        AddLog("battleship_rating_update",
            winner.User.Name, winnerChange.OldRating, winnerChange.NewRating, winnerChange.Delta,
            loser.User.Name, loserChange.OldRating, loserChange.NewRating, loserChange.Delta);

        await RenderPublicPanel();
        await RenderPlayerPages();
    }

    private void AddLog(string key, params object[] formatArguments)
    {
        _log.Add(Context.GetString(key, formatArguments));
    }

    private async Task RenderPublicPanel()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/Battleship/BattleshipStatus", BuildModel(null));
        Context.SendUpdatableHtml(AnnounceId, template.RemoveNewlines(), true);
    }

    private async Task RenderPlayerPages()
    {
        foreach (var player in _players)
        {
            await RenderPlayerPage(player);
        }
    }

    private async Task RenderPlayerPage(BattleshipPlayer player)
    {
        var templateKey = _phase == BattleshipPhase.Placement
            ? "Games/Battleship/BattleshipPlacement"
            : "Games/Battleship/BattleshipPlayerView";
        var template = await _templatesManager.GetTemplateAsync(templateKey, BuildModel(player));
        Context.SendHtmlPageTo(player.User.UserId, PlayerPageId, template.RemoveNewlines());
    }

    private bool TryParseCoordinate(string coordinate, out int row, out int column)
    {
        row = -1;
        column = -1;

        if (string.IsNullOrWhiteSpace(coordinate))
        {
            return false;
        }

        var trimmed = coordinate.Trim().ToUpperInvariant();
        if (trimmed.Length < 2)
        {
            return false;
        }

        var columnLabel = trimmed[0];
        if (columnLabel is < 'A' or > 'Z')
        {
            return false;
        }

        column = columnLabel - 'A';
        if (column >= BattleshipConstants.BOARD_SIZE)
        {
            return false;
        }

        if (!int.TryParse(trimmed[1..], out var rowNumber))
        {
            return false;
        }

        row = rowNumber - 1;
        return row >= 0 && row < BattleshipConstants.BOARD_SIZE;
    }

    private BattleshipModel BuildModel(BattleshipPlayer viewer)
    {
        var nextShip = viewer is not null && _phase == BattleshipPhase.Placement && !viewer.HasPlacedAllShips
            ? BattleshipConstants.FLEET[viewer.NextShipIndex]
            : null;

        return new BattleshipModel
        {
            Culture = Context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = Context.RoomId,
            CurrentGame = this,
            Viewer = viewer,
            Opponent = viewer is null ? null : _players.FirstOrDefault(player => !Equals(player.User, viewer.User)),
            IsPlacementPhase = _phase == BattleshipPhase.Placement,
            ViewerNextShip = nextShip,
            ViewerIsHorizontal = viewer?.IsHorizontalPlacement ?? true,
            ViewerHasPlacedAllShips = viewer?.HasPlacedAllShips ?? false,
            Log = _log,
            WinnerName = WinnerName
        };
    }

    #endregion
}
