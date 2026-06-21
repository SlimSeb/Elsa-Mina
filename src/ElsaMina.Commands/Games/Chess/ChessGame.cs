using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.Chess;

public class ChessGame : Game, IChessGame
{
    private static int NextGameId { get; set; } = 1;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IChessRatingService _ratingService;

    private readonly SemaphoreSlim _joinSemaphore = new(1, 1);
    private readonly List<IUser> _players = [];
    private readonly TimeSpan _initialClock;
    private readonly TimeSpan _increment;

    private PeriodicTimerRunner _turnTimeoutTimer;
    private TimeSpan _whiteTimeRemaining;
    private TimeSpan _blackTimeRemaining;
    private DateTime _turnStartedAtUtc;
    private bool _ended;
    private int _renderCount;

    [UsedImplicitly]
    public ChessGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IChessRatingService ratingService) : this(randomService, templatesManager, configuration,
        ratingService, ChessConstants.INITIAL_CLOCK)
    {
    }

    public ChessGame(IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IChessRatingService ratingService,
        TimeSpan initialClock)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _ratingService = ratingService;
        _initialClock = initialClock;
        _increment = ChessConstants.CLOCK_INCREMENT;

        GameId = NextGameId++;
    }

    #region Properties

    public ChessBoard Board { get; } = new();

    public IReadOnlyList<IUser> Players => _players;

    public IUser WhitePlayer => _players.Count > 0 ? _players[0] : null;

    public IUser BlackPlayer => _players.Count > 1 ? _players[1] : null;

    public IUser PlayerCurrentlyPlaying => Board.WhiteToMove ? WhitePlayer : BlackPlayer;

    public int TurnCount { get; private set; }

    public TimeSpan WhiteTimeRemaining => GetTimeRemaining(forWhite: true);

    public TimeSpan BlackTimeRemaining => GetTimeRemaining(forWhite: false);

    public int GameId { get; }

    public (int Row, int Column)? SelectedSquare { get; private set; }

    public IReadOnlyCollection<(int Row, int Column)> SelectedSquareDestinations { get; private set; } = [];

    public override string Identifier => nameof(ChessGame);

    public string PlayerNames => string.Join(", ", _players.Select(player => player.Name));

    public IContext Context { get; set; }

    private string AnnounceId => $"chess-announce-{GameId}";

    private string PublicBoardId => $"chess-game-{Context.RoomId}-{GameId}";

    private string PlayerPageId => $"chess-{GameId}";

    #endregion

    #region Public Methods

    public async Task DisplayAnnounce()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/Chess/ChessGamePanel",
            new ChessModel
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
            if (IsStarted || _players.Count >= ChessConstants.MAX_PLAYERS_COUNT)
            {
                return;
            }

            if (_players.Contains(user))
            {
                return;
            }

            _players.Add(user);
            if (_players.Count >= ChessConstants.MAX_PLAYERS_COUNT)
            {
                await StartGame();
            }
        }
        finally
        {
            _joinSemaphore.Release();
        }
    }

    public async Task Play(IUser user, string input)
    {
        if (!IsStarted || !Equals(user, PlayerCurrentlyPlaying) || string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        input = input.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");

        // A single square toggles the selection used by the clickable board.
        if (ChessBoard.TryParseSquare(input, out var selectedRow, out var selectedColumn))
        {
            UpdateSelection(selectedRow, selectedColumn);
            await RenderBoard();
            return;
        }

        var move = ResolveMove(input);
        if (move is null)
        {
            return;
        }

        DeductElapsedTime();
        Board.ApplyMove(move);
        SelectedSquare = null;
        SelectedSquareDestinations = [];
        TurnCount++;

        var opponentIsWhite = Board.WhiteToMove;
        if (Board.IsCheckmate(opponentIsWhite))
        {
            var loser = PlayerCurrentlyPlaying;
            await OnWin(user, loser);
            return;
        }

        if (Board.IsStalemate(opponentIsWhite))
        {
            await OnDraw();
            return;
        }

        await RenderBoard();
        ScheduleTurnTimeout();
    }

    public async Task Forfeit(IUser user)
    {
        if (!IsStarted || !_players.Contains(user))
        {
            return;
        }

        Context.ReplyLocalizedMessage("chess_game_player_forfeited", user.Name);
        var winner = _players.FirstOrDefault(player => !Equals(player, user));
        await OnWin(winner, user);
    }

    public async Task OnTimeout()
    {
        if (IsEnded)
        {
            return;
        }

        if (Board.WhiteToMove)
        {
            _whiteTimeRemaining = TimeSpan.Zero;
        }
        else
        {
            _blackTimeRemaining = TimeSpan.Zero;
        }

        Context.ReplyLocalizedMessage("chess_game_on_timeout", PlayerCurrentlyPlaying.Name);
        var loser = PlayerCurrentlyPlaying;
        var winner = _players.FirstOrDefault(player => !Equals(player, loser));
        await OnWin(winner, loser);
    }

    public void Cancel()
    {
        OnEnd();
        _turnTimeoutTimer?.Stop();
    }

    #endregion

    #region Private Methods

    private void UpdateSelection(int row, int column)
    {
        if (SelectedSquare == (row, column))
        {
            SelectedSquare = null;
            SelectedSquareDestinations = [];
            return;
        }

        var piece = Board.Squares[row, column];
        if (piece == ChessBoard.EMPTY || ChessBoard.IsWhite(piece) != Board.WhiteToMove)
        {
            SelectedSquare = null;
            SelectedSquareDestinations = [];
            return;
        }

        SelectedSquare = (row, column);
        SelectedSquareDestinations = Board.GenerateLegalMoves(Board.WhiteToMove)
            .Where(move => move.FromRow == row && move.FromColumn == column)
            .Select(move => (move.ToRow, move.ToColumn))
            .Distinct()
            .ToList();
    }

    private ChessMove ResolveMove(string input)
    {
        if (input.Length is < 4 or > 5)
        {
            return null;
        }

        if (!ChessBoard.TryParseSquare(input[..2], out var fromRow, out var fromColumn)
            || !ChessBoard.TryParseSquare(input.Substring(2, 2), out var toRow, out var toColumn))
        {
            return null;
        }

        var promotion = input.Length == 5 ? input[4] : ChessBoard.EMPTY;

        var candidates = Board.GenerateLegalMoves(Board.WhiteToMove)
            .Where(move => move.FromRow == fromRow
                           && move.FromColumn == fromColumn
                           && move.ToRow == toRow
                           && move.ToColumn == toColumn)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // Several candidates means the move is a promotion: pick the requested piece, default to queen.
        var requested = promotion == ChessBoard.EMPTY ? 'q' : char.ToLowerInvariant(promotion);
        return candidates.FirstOrDefault(move => char.ToLowerInvariant(move.Promotion) == requested);
    }

    private async Task StartGame()
    {
        var ongoingGameMessage = Context.GetString("chess_panel_ongoing_game", PlayerNames);
        Context.SendUpdatableHtml(AnnounceId, ongoingGameMessage, true);

        Board.Initialize();
        OnStart();
        _randomService.ShuffleInPlace(_players);
        _whiteTimeRemaining = _initialClock;
        _blackTimeRemaining = _initialClock;
        await RenderBoard();
        ScheduleTurnTimeout();
    }

    private void DeductElapsedTime()
    {
        var elapsed = DateTime.UtcNow - _turnStartedAtUtc;
        if (Board.WhiteToMove)
        {
            _whiteTimeRemaining = _whiteTimeRemaining - elapsed + _increment;
        }
        else
        {
            _blackTimeRemaining = _blackTimeRemaining - elapsed + _increment;
        }
    }

    private void ScheduleTurnTimeout()
    {
        _turnTimeoutTimer?.Stop();

        var remaining = Board.WhiteToMove ? _whiteTimeRemaining : _blackTimeRemaining;
        if (remaining <= TimeSpan.Zero)
        {
            remaining = TimeSpan.FromMilliseconds(1);
        }

        _turnStartedAtUtc = DateTime.UtcNow;
        _turnTimeoutTimer = new PeriodicTimerRunner(remaining, OnTimeout, runOnce: true);
        _turnTimeoutTimer.Start();
    }

    private TimeSpan GetTimeRemaining(bool forWhite)
    {
        var remaining = forWhite ? _whiteTimeRemaining : _blackTimeRemaining;
        if (IsStarted && !IsEnded && Board.WhiteToMove == forWhite && _turnStartedAtUtc != default)
        {
            remaining -= DateTime.UtcNow - _turnStartedAtUtc;
        }

        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private async Task OnWin(IUser winner, IUser loser)
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        await RenderBoard();

        Context.ReplyLocalizedMessage("chess_game_win_message", winner.Name);
        var (winnerChange, loserChange) = await _ratingService.UpdateRatingsOnWinAsync(winner, loser);
        Context.ReplyLocalizedMessage("chess_rating_update",
            winner.Name, winnerChange.OldRating, winnerChange.NewRating, winnerChange.Delta,
            loser.Name, loserChange.OldRating, loserChange.NewRating, loserChange.Delta);

        Cancel();
    }

    private async Task OnDraw()
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        await RenderBoard();

        Context.ReplyLocalizedMessage("chess_game_draw_end");
        var (change1, change2) = await _ratingService.UpdateRatingsOnDrawAsync(WhitePlayer, BlackPlayer);
        Context.ReplyLocalizedMessage("chess_rating_update",
            WhitePlayer.Name, change1.OldRating, change1.NewRating, change1.Delta,
            BlackPlayer.Name, change2.OldRating, change2.NewRating, change2.Delta);

        Cancel();
    }

    private async Task RenderBoard()
    {
        await RenderPublicBoard();
        await RenderPlayerPages();
    }

    /// <summary>
    /// Renders the public, spectator-only board as a chat panel in the room. It is shown from White's
    /// perspective and has no clickable squares.
    /// </summary>
    private async Task RenderPublicBoard()
    {
        _renderCount++;
        var template = await _templatesManager.GetTemplateAsync("Games/Chess/ChessGameTable",
            BuildModel(viewerColor: null));

        Context.SendUpdatableHtml(PublicBoardId, template.RemoveNewlines(), _renderCount > 1 || _ended);
    }

    /// <summary>
    /// Renders each player's private HTML page with the board oriented so their own pieces sit at the
    /// bottom. Only the player to move gets clickable squares.
    /// </summary>
    private async Task RenderPlayerPages()
    {
        await RenderPlayerPage(WhitePlayer, ChessColor.White);
        await RenderPlayerPage(BlackPlayer, ChessColor.Black);
    }

    private async Task RenderPlayerPage(IUser player, ChessColor color)
    {
        if (player is null)
        {
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Games/Chess/ChessGameTable",
            BuildModel(color));

        Context.SendHtmlPageTo(player.UserId, PlayerPageId, template.RemoveNewlines());
    }

    private ChessModel BuildModel(ChessColor? viewerColor) => new()
    {
        Culture = Context.Culture,
        RoomId = Context.RoomId,
        CurrentGame = this,
        BotName = _configuration.Name,
        Trigger = _configuration.Trigger,
        ViewerColor = viewerColor
    };

    #endregion
}
