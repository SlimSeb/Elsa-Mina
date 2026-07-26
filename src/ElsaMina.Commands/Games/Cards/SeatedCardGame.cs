using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// The scaffolding every seated card game needs around its own rules: a lobby that fills up, a table
/// of seats with a turn moving around it, a lock that serializes incoming actions, a turn timer with
/// its warning, and the public chat panel the room follows the game on.
/// </summary>
/// <remarks>
/// Deliberately not generic in the card type: the games share no card model, and
/// <see cref="IsAcceptingActions"/> lets each keep its own phase enum while still answering the one
/// question the shared flows need to ask.
/// </remarks>
/// <typeparam name="TPlayer">The game's own seat type.</typeparam>
public abstract class SeatedCardGame<TPlayer> : Game, ICardGame where TPlayer : class, ISeatedPlayer
{
    // Generic statics are per closed type, so each game keeps its own counter.
    private static int _nextGameId;

    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly PeriodicTimerRunner _turnTimer;
    private readonly PeriodicTimerRunner _turnWarningTimer;
    private readonly int _turnWarningSeconds;
    private readonly List<TPlayer> _players = [];

    protected SeatedCardGame(ITemplatesManager templatesManager, TimeSpan turnTimeout,
        TimeSpan? turnWarningRemaining)
    {
        TemplatesManager = templatesManager;
        GameId = Interlocked.Increment(ref _nextGameId);
        _turnTimer = new PeriodicTimerRunner(turnTimeout, OnTurnTimerTickAsync, runOnce: true);

        // Warn the active player by PM once only the warning threshold of time is left on their turn.
        var warningDelay = turnTimeout - (turnWarningRemaining ?? turnTimeout);
        if (turnWarningRemaining is not null && warningDelay > TimeSpan.Zero)
        {
            _turnWarningSeconds = (int)turnWarningRemaining.Value.TotalSeconds;
            _turnWarningTimer = new PeriodicTimerRunner(warningDelay, OnTurnWarningTickAsync, runOnce: true);
        }
    }

    public int GameId { get; }
    public IContext Context { get; set; }

    public IReadOnlyList<TPlayer> Players => _players;
    public int PlayerCount => _players.Count;

    public abstract bool IsInLobby { get; }

    public bool HasPlayer(string userId) => _players.Any(player => player.UserId == userId);

    protected ITemplatesManager TemplatesManager { get; }

    /// <summary>
    /// The seats, in turn order. Writable so a game can shuffle the seating before dealing.
    /// </summary>
    protected List<TPlayer> Seats => _players;

    /// <summary>
    /// The prefix every resource key of this game starts with, e.g. <c>"tarot"</c>.
    /// </summary>
    protected abstract string ResourcePrefix { get; }

    /// <summary>
    /// The folder its templates live in under <c>Games/</c>, and the prefix of their file names,
    /// e.g. <c>"Tarot"</c> for <c>Games/Tarot/TarotTable</c>.
    /// </summary>
    protected abstract string TemplateFolder { get; }

    /// <summary>
    /// Whether the game is in a phase where a player is expected to act, which is what decides
    /// whether the turn timer runs.
    /// </summary>
    protected abstract bool IsAcceptingActions { get; }

    /// <summary>
    /// Whether the game is over and showing its result.
    /// </summary>
    protected abstract bool IsFinished { get; }

    protected abstract int MinPlayers { get; }
    protected abstract int MaxPlayers { get; }

    protected abstract TPlayer CreatePlayer(IUser user);

    /// <summary>
    /// The view model handed to every template of this game.
    /// </summary>
    protected abstract object BuildModel(TPlayer viewer);

    /// <summary>
    /// Deals the first hand once the lobby has closed.
    /// </summary>
    protected abstract Task StartDealAsync();

    /// <summary>
    /// Acts for whoever let their turn run out.
    /// </summary>
    /// <remarks>Called with the action lock already held: do not take it again.</remarks>
    protected abstract Task OnTurnTimeoutAsync();

    protected string Key(string suffix) => $"{ResourcePrefix}_{suffix}";

    protected string TemplateKey(string name) => $"Games/{TemplateFolder}/{TemplateFolder}{name}";

    protected virtual string PublicPanelId => $"{ResourcePrefix}-{GameId}";
    protected string PlayerPageId => $"{ResourcePrefix}-{GameId}";
    protected string SubPanelId => $"{ResourcePrefix}-{GameId}-sub";
    protected string LogPanelId => $"{ResourcePrefix}-{GameId}-log";

    #region Seats

    protected int CurrentTurnIndex { get; set; }

    /// <summary>
    /// The seat whose turn it is, or <c>null</c> when the table is empty.
    /// </summary>
    protected TPlayer CurrentSeat =>
        CurrentTurnIndex >= 0 && CurrentTurnIndex < _players.Count ? _players[CurrentTurnIndex] : null;

    /// <summary>
    /// Moves the turn to the next seat around the table.
    /// </summary>
    protected void AdvanceTurn() => CurrentTurnIndex = (CurrentTurnIndex + 1) % _players.Count;

    protected TPlayer FindSeat(string userId) =>
        _players.FirstOrDefault(player => player.UserId == userId);

    protected int SeatIndexOf(TPlayer player) => _players.IndexOf(player);

    /// <summary>
    /// The seat <paramref name="offset"/> places clockwise from <paramref name="from"/>.
    /// </summary>
    protected TPlayer SeatFrom(int from, int offset) => _players[(from + offset) % _players.Count];

    #endregion

    #region Lobby

    public Task BeginJoinPhaseAsync() => RenderPublicAsync();

    public async Task<(bool Success, string MessageKey, object[] Args)> JoinAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (!IsInLobby)
            {
                return (false, Key("join_already_started"), []);
            }

            if (_players.Count >= MaxPlayers)
            {
                return (false, Key("join_full"), []);
            }

            if (_players.Any(player => player.UserId == user.UserId))
            {
                return (false, Key("join_already_joined"), []);
            }

            var rejection = await OnJoiningAsync(user);
            if (rejection is not null)
            {
                return rejection.Value;
            }

            _players.Add(CreatePlayer(user));
            await RenderPublicAsync();
            return (true, Key("join_success"), JoinSuccessArguments(user));
        }
        finally
        {
            _actionLock.Release();
        }
    }

    /// <summary>
    /// A last admission check, run once a seat is known to be available. Poker uses it to take the
    /// buy-in out of the player's bucks. Returning a value rejects the join with that message.
    /// </summary>
    protected virtual Task<(bool Success, string MessageKey, object[] Args)?> OnJoiningAsync(IUser user) =>
        Task.FromResult<(bool Success, string MessageKey, object[] Args)?>(null);

    protected virtual object[] JoinSuccessArguments(IUser user) => [user.Name];

    /// <summary>
    /// Gives a seat back while the game is still gathering players. Only exposed by the games whose
    /// interface declares it: belote's table is either exactly full or not startable at all.
    /// </summary>
    protected async Task<(bool Success, string MessageKey, object[] Args)> LeaveSeatAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (!IsInLobby)
            {
                return (false, Key("quit_already_started"), []);
            }

            var player = FindSeat(user.UserId);
            if (player is null)
            {
                return (false, Key("quit_not_joined"), []);
            }

            _players.Remove(player);
            await RenderPublicAsync();
            return (true, Key("quit_success"), [player.Name]);
        }
        finally
        {
            _actionLock.Release();
        }
    }

    public async Task StartAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (!IsInLobby)
            {
                Context.ReplyLocalizedMessage(Key("start_already_started"));
                return;
            }

            if (!CanStart(user))
            {
                return;
            }

            if (_players.Count < MinPlayers)
            {
                Context.ReplyLocalizedMessage(Key("start_not_enough_players"), MinPlayers);
                return;
            }

            await StartDealAsync();
        }
        finally
        {
            _actionLock.Release();
        }
    }

    /// <summary>
    /// An extra guard applied before the player count is checked, with the rejection message left to
    /// the game. Poker uses it to refuse a start from someone who is not seated.
    /// </summary>
    protected virtual bool CanStart(IUser user) => true;

    public abstract Task CancelAsync();

    #endregion

    #region Action lock & timers

    /// <summary>
    /// Runs an action with the game locked, so two commands arriving at once are applied one after the
    /// other and the second sees what the first wrote.
    /// </summary>
    protected async Task RunActionAsync(Func<Task> action)
    {
        await _actionLock.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            _actionLock.Release();
        }
    }

    protected void RestartTurnTimer()
    {
        if (IsAcceptingActions)
        {
            _turnTimer.Restart();
            _turnWarningTimer?.Restart();
        }
    }

    protected void StopTurnTimer()
    {
        _turnTimer.Stop();
        _turnWarningTimer?.Stop();
    }

    /// <summary>
    /// Who to warn by PM shortly before the turn runs out. Usually the player on turn, but président
    /// warns every player who still owes cards during its exchange.
    /// </summary>
    protected virtual IEnumerable<TPlayer> GetTurnWarningRecipients() => [];

    private async Task OnTurnTimerTickAsync()
    {
        await _actionLock.WaitAsync();
        try
        {
            await OnTurnTimeoutAsync();
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private async Task OnTurnWarningTickAsync()
    {
        await _actionLock.WaitAsync();
        try
        {
            var recipients = GetTurnWarningRecipients().ToList();
            if (recipients.Count == 0)
            {
                return;
            }

            var message = Context.GetString(Key("turn_timeout_warning"), _turnWarningSeconds);
            foreach (var player in recipients)
            {
                Context.SendMessageIn(Context.RoomId, $"/pm {player.UserId}, {message}");
            }
        }
        finally
        {
            _actionLock.Release();
        }
    }

    #endregion

    #region Public panel

    /// <summary>
    /// True once the public panel has been posted, so further renders update it in place.
    /// </summary>
    protected bool PublicPanelInitialized { get; set; }

    /// <summary>
    /// Renders the public table as a chat panel so spectators (and players) can follow the game from
    /// the room itself. The lobby and the final result are only ever shown here. When
    /// <paramref name="forceResend"/> is set, it is re-posted at the bottom of the chat instead of
    /// updated in place high up in the scrollback.
    /// </summary>
    protected async Task RenderPublicAsync(bool forceResend = false)
    {
        var templateKey = TemplateKey(IsInLobby ? "Lobby" : IsFinished ? "Result" : "Table");

        var html = await TemplatesManager.GetTemplateAsync(templateKey, BuildModel(null));
        Context.SendUpdatableHtml(PublicPanelId, html.RemoveNewlines(),
            isChanging: PublicPanelInitialized && !forceResend);
        PublicPanelInitialized = true;
    }

    /// <summary>
    /// Replaces the lobby panel (with its join and start buttons) by a clear "cancelled" notice, so it
    /// does not look like the game is still open.
    /// </summary>
    protected async Task RenderCancelledPublicAsync()
    {
        var html = await TemplatesManager.GetTemplateAsync(TemplateKey("Cancelled"), BuildModel(null));
        Context.SendUpdatableHtml(PublicPanelId, html.RemoveNewlines(), PublicPanelInitialized);
        PublicPanelInitialized = true;
    }

    /// <summary>
    /// Wipes the public panel so the next render posts a fresh one at the bottom of the chat.
    /// </summary>
    protected void WipePublicPanel()
    {
        Context.SendUpdatableHtml(PublicPanelId, string.Empty, true);
        PublicPanelInitialized = false;
    }

    #endregion
}
