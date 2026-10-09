using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.ConnectFour;

public class ConnectFourGame : Game, IConnectFourGame
{
    private const char DEFAULT_CHARACTER = '_';

    private static int NextGameId { get; set; } = 1;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IConnectFourRatingService _ratingService;

    private readonly PeriodicTimerRunner _turnTimeoutTimer;
    private readonly SemaphoreSlim _joinSemaphore = new(1, 1);
    private bool _ended;

    [UsedImplicitly]
    public ConnectFourGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IConnectFourRatingService ratingService) : this(randomService, templatesManager, configuration,
        ratingService, ConnectFourConstants.TIMEOUT_DELAY)
    {
    }

    public ConnectFourGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IConnectFourRatingService ratingService,
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

    public List<IUser> Players { get; } = [];

    public List<(int, int)> WinningLineIndices { get; private set; } = [];

    public IUser PlayerCurrentlyPlaying { get; private set; }

    public char CurrentPlayerSymbol { get; private set; }

    public int TurnCount { get; private set; }

    public (int, int) LastPlayIndices { get; private set; } = (-1, -1);

    public char[,] Grid { get; } = new char[ConnectFourConstants.GRID_HEIGHT, ConnectFourConstants.GRID_WIDTH];

    public override string Identifier => nameof(ConnectFourGame);

    public string PlayerNames => string.Join(", ", Players.Select(player => player.Name));

    public int GameId { get; }

    public IContext Context { get; set; }

    private string AnnounceId => $"c4-announce-{GameId}";

    #endregion

    #region Public Methods

    public async Task DisplayAnnounce()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/ConnectFour/ConnectFourGamePanel",
            new ConnectFourModel
            {
                Culture = Context.Culture,
                BotName = _configuration.Name,
                CurrentGame = this,
                RoomId = Context.RoomId,
                Trigger = _configuration.Trigger
            });

        Context.SendUpdatableHtml(AnnounceId, template.RemoveNewlines(), false);
    }

    public async Task JoinGame(IUser user)
    {
        await _joinSemaphore.WaitAsync();
        try
        {
            if (IsStarted || Players.Count >= ConnectFourConstants.MAX_PLAYERS_COUNT)
            {
                return;
            }

            if (Players.Contains(user))
            {
                return;
            }

            Players.Add(user);
            if (Players.Count >= ConnectFourConstants.MAX_PLAYERS_COUNT)
            {
                await StartGame();
            }
        }
        finally
        {
            _joinSemaphore.Release();
        }
    }

    public async Task Play(IUser user, string playedColumn)
    {
        if (!IsStarted || !Equals(user, PlayerCurrentlyPlaying))
        {
            return;
        }

        if (!int.TryParse(playedColumn, out var playedColumnIndex))
        {
            return;
        }

        if (playedColumnIndex is < 1 or > ConnectFourConstants.GRID_WIDTH)
        {
            return;
        }

        playedColumnIndex -= 1;
        var i = ConnectFourConstants.GRID_HEIGHT - 1;
        while (i >= 0 && Grid[i, playedColumnIndex] != DEFAULT_CHARACTER)
        {
            i--;
        }

        if (i < 0)
        {
            return; // La colonne est remplie
        }

        Grid[i, playedColumnIndex] = CurrentPlayerSymbol;
        LastPlayIndices = (i, playedColumnIndex);

        foreach (var symbol in ConnectFourConstants.SYMBOLS)
        {
            await CheckWin(symbol);
            if (_ended)
            {
                return;
            }
        }
        
        if (CheckTie())
        {
            await OnWin(Players[0], Players[1], true);
            return;
        }

        await InitializeNextTurn();
    }

    public async Task Forfeit(IUser user)
    {
        if (!IsStarted || !Players.Contains(user))
        {
            return;
        }

        Context.ReplyLocalizedMessage("c4_game_player_forfeited", user.Name);
        var winner = Players.FirstOrDefault(player => !Equals(player, user));
        await OnWin(winner, user);
    }

    public async Task OnTimeout()
    {
        if (IsEnded)
        {
            return;
        }

        Context.ReplyLocalizedMessage("c4_game_on_timeout", PlayerCurrentlyPlaying.Name);
        var loser = PlayerCurrentlyPlaying;
        var winner = Players.FirstOrDefault(player => !Equals(player, loser));
        await OnWin(winner, loser);
    }

    public void Cancel()
    {
        OnEnd();
        _turnTimeoutTimer.Stop();
    }

    #endregion

    #region Private Methods

    private async Task CheckWin(char symbol)
    {
        List<List<(int, int)>> checks =
        [
            CheckWinDirection(symbol, 0, 1), // Horizontal
            CheckWinDirection(symbol, 1, 0), // Vertical
            CheckWinDirection(symbol, 1, 1), // Diagonal (top-left to bottom-right)
            CheckWinDirection(symbol, 1, -1) // Diagonal (top-right to bottom-left)
        ];
        var passingChecks = checks.Where(check => check.Count > 0).ToArray();

        if (passingChecks.Length >= 1)
        {
            WinningLineIndices = passingChecks[0];
            var winner = Players[Array.IndexOf(ConnectFourConstants.SYMBOLS, symbol)];
            var loser = Players.FirstOrDefault(player => !Equals(player, winner));
            await OnWin(winner, loser);
        }
    }

    private bool CheckTie()
    {
        for (var i = 0; i < ConnectFourConstants.GRID_HEIGHT; i++)
        {
            for (var j = 0; j < ConnectFourConstants.GRID_WIDTH; j++)
            {
                if (Grid[i, j] == DEFAULT_CHARACTER)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private List<(int, int)> CheckWinDirection(char symbol, int rowOffset, int colOffset)
    {
        for (var i = 0; i < ConnectFourConstants.GRID_HEIGHT; i++)
        {
            for (var j = 0; j < ConnectFourConstants.GRID_WIDTH; j++)
            {
                var currentIndices = CollectWinningLine(symbol, i, j, rowOffset, colOffset);
                if (currentIndices.Count == ConnectFourConstants.WINNING_LENGTH)
                {
                    return currentIndices;
                }
            }
        }

        return [];
    }

    /// <summary>
    /// The cells of the run of <see cref="ConnectFourConstants.WINNING_LENGTH"/> cells starting at the
    /// given cell and walking in the given direction, or an empty list when the run leaves the grid or
    /// hits a cell that does not belong to the given symbol.
    /// </summary>
    private List<(int, int)> CollectWinningLine(char symbol, int startRow, int startColumn, int rowOffset,
        int colOffset)
    {
        var currentIndices = new List<(int, int)>();
        for (var k = 0; k < ConnectFourConstants.WINNING_LENGTH; k++)
        {
            var row = startRow + k * rowOffset;
            var col = startColumn + k * colOffset;

            if (!IsInsideGrid(row, col) || Grid[row, col] != symbol)
            {
                return [];
            }

            currentIndices.Add((row, col));
        }

        return currentIndices;
    }

    private static bool IsInsideGrid(int row, int col) =>
        row >= 0 && row < ConnectFourConstants.GRID_HEIGHT && col >= 0 && col < ConnectFourConstants.GRID_WIDTH;

    private async Task StartGame()
    {
        var ongoingGameMessage = Context.GetString("c4_panel_ongoing_game", PlayerNames);
        Context.SendUpdatableHtml(AnnounceId, ongoingGameMessage, true);

        for (var i = 0; i < ConnectFourConstants.GRID_HEIGHT; i++)
        {
            for (var j = 0; j < ConnectFourConstants.GRID_WIDTH; j++)
            {
                Grid[i, j] = DEFAULT_CHARACTER;
            }
        }

        OnStart();
        _randomService.ShuffleInPlace(Players);
        await InitializeNextTurn();
    }

    private async Task InitializeNextTurn()
    {
        TurnCount++;
        PlayerCurrentlyPlaying = Players[(TurnCount - 1) % ConnectFourConstants.MAX_PLAYERS_COUNT];
        CurrentPlayerSymbol = ConnectFourConstants.SYMBOLS[Players.IndexOf(PlayerCurrentlyPlaying)];
        await SendGridToPlayers();

        _turnTimeoutTimer.Restart();
    }

    private async Task OnWin(IUser winner, IUser loser, bool isTie = false)
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        await SendGridToPlayers();

        if (isTie)
        {
            Context.ReplyLocalizedMessage("c4_game_tie_end");
            var (change1, change2) = await _ratingService.UpdateRatingsOnDrawAsync(winner, loser);
            Context.ReplyLocalizedMessage("c4_rating_update",
                winner.Name, change1.OldRating, change1.NewRating, change1.Delta,
                loser.Name, change2.OldRating, change2.NewRating, change2.Delta);
        }
        else
        {
            Players.Remove(loser);
            Context.ReplyLocalizedMessage("c4_game_win_message", winner.Name);
            var (winnerChange, loserChange) = await _ratingService.UpdateRatingsOnWinAsync(winner, loser);
            Context.ReplyLocalizedMessage("c4_rating_update",
                winner.Name, winnerChange.OldRating, winnerChange.NewRating, winnerChange.Delta,
                loser.Name, loserChange.OldRating, loserChange.NewRating, loserChange.Delta);
        }

        Cancel();
    }

    private async Task SendGridToPlayers()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/ConnectFour/ConnectFourGameTable",
            new ConnectFourModel
            {
                Culture = Context.Culture,
                RoomId = Context.RoomId,
                CurrentGame = this,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger
            });

        var gridId = $"c4-game-{Context.RoomId}-{GameId}";
        Context.SendUpdatableHtml(gridId, template.RemoveNewlines(), TurnCount > 1 || _ended);
    }

    #endregion
}