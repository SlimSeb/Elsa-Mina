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
    private readonly SemaphoreSlim _joinSemaphore = new(1, 1);
    private readonly List<BattleshipPlayer> _players = [];
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
        TimeSpan timeoutDelay)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _ratingService = ratingService;
        _turnTimeoutTimer = new PeriodicTimerRunner(timeoutDelay, OnTimeout, runOnce: true);

        GameId = NextGameId++;
    }

    #region Properties

    public IReadOnlyList<BattleshipPlayer> Players => _players;

    public BattleshipPlayer CurrentPlayer { get; private set; }

    public IUser PlayerCurrentlyPlaying => CurrentPlayer?.User;

    public int TurnCount { get; private set; }

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
            if (IsStarted || _players.Count >= BattleshipConstants.MAX_PLAYERS_COUNT)
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
                await StartGame();
            }
        }
        finally
        {
            _joinSemaphore.Release();
        }
    }

    public async Task Fire(IUser user, string coordinate)
    {
        if (!IsStarted || CurrentPlayer is null || !Equals(user, CurrentPlayer.User))
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
            Context.ReplyLocalizedMessage("battleship_shot_miss", user.Name,
                BattleshipConstants.FormatCoordinate(row, column));
        }
        else
        {
            board.Shots[row, column] = CellShotState.Hit;
            ship.Hits++;
            if (ship.IsSunk)
            {
                Context.ReplyLocalizedMessage("battleship_shot_sunk", user.Name, opponent.User.Name,
                    Context.GetString($"battleship_ship_{ship.NameKey}"));
            }
            else
            {
                Context.ReplyLocalizedMessage("battleship_shot_hit", user.Name,
                    BattleshipConstants.FormatCoordinate(row, column));
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
        if (!IsStarted)
        {
            return;
        }

        var loser = _players.FirstOrDefault(player => Equals(player.User, user));
        if (loser is null)
        {
            return;
        }

        Context.ReplyLocalizedMessage("battleship_player_forfeited", user.Name);
        var winner = _players.First(player => !Equals(player.User, user));
        await FinishAsync(winner, loser);
    }

    public async Task OnTimeout()
    {
        if (IsEnded || CurrentPlayer is null)
        {
            return;
        }

        Context.ReplyLocalizedMessage("battleship_on_timeout", CurrentPlayer.User.Name);
        var loser = CurrentPlayer;
        var winner = _players.First(player => !Equals(player.User, loser.User));
        await FinishAsync(winner, loser);
    }

    public void Cancel()
    {
        OnEnd();
        _turnTimeoutTimer.Stop();
        Context.SendUpdatableHtml(AnnounceId, string.Empty, true);
        foreach (var player in _players)
        {
            Context.CloseHtmlPage(player.User.UserId, PlayerPageId);
        }
    }

    #endregion

    #region Private Methods

    private async Task StartGame()
    {
        var ongoingGameMessage = Context.GetString("battleship_panel_ongoing_game", PlayerNames);
        Context.SendUpdatableHtml(AnnounceId, ongoingGameMessage, true);

        foreach (var player in _players)
        {
            PlaceFleet(player.Board);
        }

        OnStart();
        _randomService.ShuffleInPlace(_players);
        await InitializeNextTurn();
    }

    private void PlaceFleet(BattleshipBoard board)
    {
        foreach (var shipType in BattleshipConstants.FLEET)
        {
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

                var cells = new List<(int Row, int Column)>();
                for (var offset = 0; offset < shipType.Size; offset++)
                {
                    cells.Add(isHorizontal
                        ? (startRow, startColumn + offset)
                        : (startRow + offset, startColumn));
                }

                if (cells.Any(cell => board.ShipGrid[cell.Row, cell.Column] is not null))
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

    private async Task InitializeNextTurn()
    {
        TurnCount++;
        CurrentPlayer = _players[(TurnCount - 1) % BattleshipConstants.MAX_PLAYERS_COUNT];
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
        OnEnd();
        _turnTimeoutTimer.Stop();

        await RenderPlayerPages();

        Context.ReplyLocalizedMessage("battleship_win_message", winner.User.Name);
        var (winnerChange, loserChange) = await _ratingService.UpdateRatingsOnWinAsync(winner.User, loser.User);
        Context.ReplyLocalizedMessage("battleship_rating_update",
            winner.User.Name, winnerChange.OldRating, winnerChange.NewRating, winnerChange.Delta,
            loser.User.Name, loserChange.OldRating, loserChange.NewRating, loserChange.Delta);

        Context.SendUpdatableHtml(AnnounceId, string.Empty, true);
    }

    private async Task RenderPlayerPages()
    {
        foreach (var player in _players)
        {
            var template = await _templatesManager.GetTemplateAsync("Games/Battleship/BattleshipPlayerView",
                BuildModel(player));
            Context.SendHtmlPageTo(player.User.UserId, PlayerPageId, template.RemoveNewlines());
        }
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

    private BattleshipModel BuildModel(BattleshipPlayer viewer) => new()
    {
        Culture = Context.Culture,
        BotName = _configuration.Name,
        Trigger = _configuration.Trigger,
        RoomId = Context.RoomId,
        CurrentGame = this,
        Viewer = viewer,
        Opponent = viewer is null ? null : _players.FirstOrDefault(player => !Equals(player.User, viewer.User))
    };

    #endregion
}
