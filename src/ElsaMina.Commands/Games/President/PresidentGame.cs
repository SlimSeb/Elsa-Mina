using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.President;

public class PresidentGame : Game, IPresidentGame
{
    private static int _nextGameId;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly PeriodicTimerRunner _turnTimer;
    private readonly PeriodicTimerRunner _turnWarningTimer;
    private readonly List<PresidentPlayer> _players = [];
    private readonly List<PresidentPlayer> _finishOrder = [];
    private readonly List<string> _log = [];

    private int _currentTurnIndex;
    private bool _publicPanelInitialized;
    private bool _subPanelInitialized;
    private bool _logPanelInitialized;
    private int _renderedLogCount;

    [UsedImplicitly]
    public PresidentGame(IRandomService randomService, ITemplatesManager templatesManager,
        IConfiguration configuration)
        : this(randomService, templatesManager, configuration, PresidentConstants.TURN_TIMEOUT)
    {
    }

    public PresidentGame(IRandomService randomService, ITemplatesManager templatesManager,
        IConfiguration configuration, TimeSpan turnTimeout)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        GameId = Interlocked.Increment(ref _nextGameId);
        _turnTimer = new PeriodicTimerRunner(turnTimeout, OnTurnTimeoutAsync, runOnce: true);

        // Warn the active player by PM once only the warning threshold of time is left on their turn.
        var warningDelay = turnTimeout - PresidentConstants.TURN_TIMEOUT_WARNING_REMAINING;
        if (warningDelay > TimeSpan.Zero)
        {
            _turnWarningTimer = new PeriodicTimerRunner(warningDelay, OnTurnWarningAsync, runOnce: true);
        }
    }

    public int GameId { get; }
    public override string Identifier => nameof(PresidentGame);

    public IContext Context { get; set; }

    public IReadOnlyList<PresidentPlayer> Players => _players;
    public int PlayerCount => _players.Count;
    public PresidentPhase Phase { get; private set; } = PresidentPhase.Lobby;

    public PresidentPlayer CurrentPlayer =>
        Phase == PresidentPhase.Playing && _currentTurnIndex >= 0 && _currentTurnIndex < _players.Count
            ? _players[_currentTurnIndex]
            : null;

    public int RoundNumber { get; private set; }
    public int TotalRounds { get; set; } = PresidentConstants.DEFAULT_ROUNDS;

    public PresidentTrick CurrentTrick { get; private set; } = new();
    public PresidentPlayer LastTrickWinner { get; private set; }
    public IReadOnlyList<PresidentPlayer> FinishOrder => _finishOrder;

    public IReadOnlyList<string> Log => _log;

    private string PublicPanelId => $"president-{GameId}";
    private string PlayerPageId => $"president-{GameId}";
    private string SubPanelId => $"president-{GameId}-sub";
    private string LogPanelId => $"president-{GameId}-log";

    private bool HasViceRoles => _players.Count >= PresidentConstants.VICE_ROLES_MIN_PLAYERS;

    #region Lobby

    public async Task BeginJoinPhaseAsync()
    {
        await RenderPublicAsync();
    }

    public async Task<(bool Success, string MessageKey, object[] Args)> JoinAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (Phase != PresidentPhase.Lobby)
            {
                return (false, "president_join_already_started", []);
            }

            if (_players.Count >= PresidentConstants.MAX_PLAYERS)
            {
                return (false, "president_join_full", []);
            }

            if (_players.Any(player => player.UserId == user.UserId))
            {
                return (false, "president_join_already_joined", []);
            }

            _players.Add(new PresidentPlayer(user));
            await RenderPublicAsync();
            return (true, "president_join_success", [user.Name]);
        }
        finally
        {
            _actionLock.Release();
        }
    }

    public async Task<(bool Success, string MessageKey, object[] Args)> LeaveAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (Phase != PresidentPhase.Lobby)
            {
                return (false, "president_quit_already_started", []);
            }

            var player = _players.FirstOrDefault(currentPlayer => currentPlayer.UserId == user.UserId);
            if (player is null)
            {
                return (false, "president_quit_not_joined", []);
            }

            _players.Remove(player);
            await RenderPublicAsync();
            return (true, "president_quit_success", [player.Name]);
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
            if (Phase != PresidentPhase.Lobby)
            {
                Context.ReplyLocalizedMessage("president_start_already_started");
                return;
            }

            if (_players.Count < PresidentConstants.MIN_PLAYERS)
            {
                Context.ReplyLocalizedMessage("president_start_not_enough_players", PresidentConstants.MIN_PLAYERS);
                return;
            }

            OnStart();
            _randomService.ShuffleInPlace(_players);
            RoundNumber = 1;
            await BeginRoundAsync();
        }
        finally
        {
            _actionLock.Release();
        }
    }

    #endregion

    #region Dealing & exchange

    /// <summary>
    /// Resets all per-round state, deals a fresh hand to everyone, then either opens the card
    /// exchange (from the second round onwards) or starts playing right away (first round).
    /// </summary>
    private async Task BeginRoundAsync()
    {
        ResetRoundState();
        DealCards();

        if (RoundNumber > 1)
        {
            ApplyAutomaticGifts();
            Phase = PresidentPhase.Exchange;
            await RenderAllAsync(resendPublic: true, resendLog: true);
            RestartTurnTimer();
            return;
        }

        await BeginPlayingAsync(_players[0]);
    }

    /// <summary>
    /// Clears every piece of per-round state so a fresh hand can be dealt over the same seating.
    /// Roles and scores survive: they drive the exchange and the final standings.
    /// </summary>
    private void ResetRoundState()
    {
        foreach (var player in _players)
        {
            player.Hand.Clear();
            player.FinishPosition = 0;
            player.HasPassed = false;
            player.CardsToGive = 0;
            player.PendingGives.Clear();
            player.ReceivedCards.Clear();
        }

        _finishOrder.Clear();
        CurrentTrick = new PresidentTrick();
        LastTrickWinner = null;
    }

    /// <summary>
    /// Deals the whole 52-card deck out round-robin; with some player counts a few players end up
    /// holding one card more than the others, which is part of the game.
    /// </summary>
    private void DealCards()
    {
        var deck = PresidentConstants.BuildDeck();
        _randomService.ShuffleInPlace(deck);

        for (var cardIndex = 0; cardIndex < deck.Count; cardIndex++)
        {
            _players[cardIndex % _players.Count].Hand.Add(deck[cardIndex]);
        }

        foreach (var player in _players)
        {
            SortHand(player.Hand);
        }
    }

    /// <summary>
    /// Applies the forced part of the exchange: the scum hands their best cards to the president
    /// (and the vice-scum their best card to the vice-president). The give-backs are then chosen
    /// freely by the receivers during the exchange phase.
    /// </summary>
    private void ApplyAutomaticGifts()
    {
        GiftBestCards(PresidentRole.Scum, PresidentRole.President, PresidentConstants.SCUM_EXCHANGE_COUNT);

        if (HasViceRoles)
        {
            GiftBestCards(PresidentRole.ViceScum, PresidentRole.VicePresident, PresidentConstants.VICE_EXCHANGE_COUNT);
        }
    }

    private void GiftBestCards(PresidentRole giverRole, PresidentRole receiverRole, int count)
    {
        var giver = FindByRole(giverRole);
        var receiver = FindByRole(receiverRole);
        if (giver is null || receiver is null)
        {
            return;
        }

        var bestCards = giver.Hand.OrderByDescending(card => card.Rank).Take(count).ToList();
        foreach (var card in bestCards)
        {
            giver.Hand.Remove(card);
            receiver.Hand.Add(card);
        }

        SortHand(receiver.Hand);
        receiver.ReceivedCards.AddRange(bestCards);
        receiver.CardsToGive = count;

        LogEvent("president_exchange_gave_best", giver.Name, count, receiver.Name);
    }

    private PresidentPlayer FindByRole(PresidentRole role) =>
        _players.FirstOrDefault(player => player.Role == role);

    public Task GiveAsync(IUser user, IReadOnlyList<PresidentCard> cards) =>
        RunActionAsync(() => GiveCoreAsync(user, cards));

    private async Task GiveCoreAsync(IUser user, IReadOnlyList<PresidentCard> cards)
    {
        if (Phase != PresidentPhase.Exchange || cards is null || cards.Count == 0)
        {
            return;
        }

        var player = _players.FirstOrDefault(currentPlayer => currentPlayer.UserId == user.UserId);
        if (player is null || player.CardsToGive == 0)
        {
            return;
        }

        // A full list (e.g. typed in one go) is applied directly; anything else toggles the selection.
        if (cards.Count == player.CardsToGive && cards.Distinct().Count() == cards.Count)
        {
            await ApplyGiveAsync(player, cards);
            return;
        }

        foreach (var card in cards)
        {
            if (!player.Hand.Contains(card))
            {
                Context.ReplyLocalizedMessage("president_give_not_in_hand");
                return;
            }

            if (!player.PendingGives.Remove(card) && player.PendingGives.Count < player.CardsToGive)
            {
                player.PendingGives.Add(card);
            }
        }

        if (player.PendingGives.Count == player.CardsToGive)
        {
            await ApplyGiveAsync(player, player.PendingGives.ToList());
            return;
        }

        await RenderPlayerPageAsync(player);
    }

    private async Task ApplyGiveAsync(PresidentPlayer giver, IReadOnlyList<PresidentCard> cards)
    {
        if (cards.Count != giver.CardsToGive
            || cards.Distinct().Count() != cards.Count
            || cards.Any(card => !giver.Hand.Contains(card)))
        {
            Context.ReplyLocalizedMessage("president_give_not_in_hand");
            return;
        }

        var receiver = FindByRole(giver.Role == PresidentRole.President
            ? PresidentRole.Scum
            : PresidentRole.ViceScum);
        if (receiver is null)
        {
            return;
        }

        foreach (var card in cards)
        {
            giver.Hand.Remove(card);
            receiver.Hand.Add(card);
        }

        SortHand(receiver.Hand);
        receiver.ReceivedCards.AddRange(cards);
        giver.PendingGives.Clear();
        giver.CardsToGive = 0;

        LogEvent("president_exchange_returned", giver.Name, cards.Count, receiver.Name);

        if (_players.All(player => player.CardsToGive == 0))
        {
            await BeginPlayingAsync(FindByRole(PresidentRole.President) ?? _players[0]);
            return;
        }

        await RenderAllAsync();
    }

    #endregion

    #region Playing

    private async Task BeginPlayingAsync(PresidentPlayer leader)
    {
        Phase = PresidentPhase.Playing;
        _currentTurnIndex = _players.IndexOf(leader);

        LogEvent("president_round_started", RoundNumber, TotalRounds, leader.Name);

        await RenderAllAsync(resendPublic: true, resendLog: true);
        RestartTurnTimer();
    }

    /// <summary>
    /// The (rank, card count) combinations the given player may legally put on the pile: any set of
    /// same-ranked cards when leading, otherwise exactly the pile's card count at an equal or higher rank.
    /// </summary>
    public IReadOnlyList<(int Rank, int Count)> GetLegalPlays(PresidentPlayer player)
    {
        if (Phase != PresidentPhase.Playing || player is null || CurrentPlayer != player)
        {
            return [];
        }

        var plays = new List<(int Rank, int Count)>();
        var rankGroups = player.Hand
            .GroupBy(card => card.Rank)
            .OrderBy(group => group.Key);

        foreach (var group in rankGroups)
        {
            var available = group.Count();
            if (CurrentTrick.IsEmpty)
            {
                for (var count = 1; count <= available; count++)
                {
                    plays.Add((group.Key, count));
                }
            }
            else if (available >= CurrentTrick.RequiredCount && group.Key >= CurrentTrick.TopRank)
            {
                plays.Add((group.Key, CurrentTrick.RequiredCount));
            }
        }

        return plays;
    }

    /// <summary>
    /// A player may pass only on their turn and never when leading a fresh pile.
    /// </summary>
    public bool CanPass(PresidentPlayer player) =>
        Phase == PresidentPhase.Playing && player is not null && CurrentPlayer == player && !CurrentTrick.IsEmpty;

    public Task PlayAsync(IUser user, int rank, int count) => RunActionAsync(() => PlayCoreAsync(user, rank, count));

    private async Task PlayCoreAsync(IUser user, int rank, int count)
    {
        if (Phase != PresidentPhase.Playing || CurrentPlayer?.UserId != user.UserId)
        {
            return;
        }

        var player = CurrentPlayer;
        if (count <= 0)
        {
            count = CurrentTrick.IsEmpty ? 1 : CurrentTrick.RequiredCount;
        }

        var matching = player.Hand.Where(card => card.Rank == rank).Take(count).ToList();
        if (matching.Count < count)
        {
            Context.ReplyLocalizedMessage("president_play_not_in_hand");
            return;
        }

        if (!CurrentTrick.IsEmpty && count != CurrentTrick.RequiredCount)
        {
            Context.ReplyLocalizedMessage("president_play_wrong_count", CurrentTrick.RequiredCount);
            return;
        }

        if (!CurrentTrick.IsEmpty && rank < CurrentTrick.TopRank)
        {
            Context.ReplyLocalizedMessage("president_play_too_low");
            return;
        }

        foreach (var card in matching)
        {
            player.Hand.Remove(card);
        }

        CurrentTrick.Add(player, matching);

        if (player.Hand.Count == 0)
        {
            _finishOrder.Add(player);
            player.FinishPosition = _finishOrder.Count;
            LogEvent("president_player_finished", player.Name, player.FinishPosition);
        }

        if (await TryFinishRoundAsync())
        {
            return;
        }

        // The 2 is unbeatable: playing it takes the pile on the spot.
        if (rank == PresidentCard.TWO)
        {
            await CloseTrickAsync(player);
            return;
        }

        await AdvanceTurnAsync(player);
    }

    public Task PassAsync(IUser user) => RunActionAsync(() => PassCoreAsync(user));

    private async Task PassCoreAsync(IUser user)
    {
        if (Phase != PresidentPhase.Playing || CurrentPlayer?.UserId != user.UserId)
        {
            return;
        }

        if (CurrentTrick.IsEmpty)
        {
            Context.ReplyLocalizedMessage("president_pass_leading");
            return;
        }

        CurrentPlayer.HasPassed = true;
        await AdvanceTurnAsync(CurrentTrick.LastPlayer);
    }

    /// <summary>
    /// Moves the turn to the next player still holding cards who has not passed on this pile. When
    /// the turn would come back to the author of the top play (everyone else passed or is out), the
    /// pile is taken instead.
    /// </summary>
    private async Task AdvanceTurnAsync(PresidentPlayer lastPlayer)
    {
        for (var offset = 1; offset <= _players.Count; offset++)
        {
            var candidate = _players[(_currentTurnIndex + offset) % _players.Count];
            if (candidate == lastPlayer)
            {
                break;
            }

            if (candidate.Hand.Count > 0 && !candidate.HasPassed)
            {
                _currentTurnIndex = _players.IndexOf(candidate);
                await RenderAllAsync();
                RestartTurnTimer();
                return;
            }
        }

        await CloseTrickAsync(lastPlayer);
    }

    /// <summary>
    /// Clears the pile and hands the lead to its winner, or to the next player still holding cards
    /// when the winner just emptied their hand.
    /// </summary>
    private async Task CloseTrickAsync(PresidentPlayer winner)
    {
        foreach (var player in _players)
        {
            player.HasPassed = false;
        }

        CurrentTrick = new PresidentTrick();
        LastTrickWinner = winner;
        LogEvent("president_trick_won", winner.Name);

        var winnerIndex = _players.IndexOf(winner);
        for (var offset = 0; offset < _players.Count; offset++)
        {
            var candidate = _players[(winnerIndex + offset) % _players.Count];
            if (candidate.Hand.Count > 0)
            {
                _currentTurnIndex = _players.IndexOf(candidate);
                break;
            }
        }

        // Force re-post the public chat panel so it drops back to the bottom of the chat instead of
        // staying stuck high up in the scrollback. Player hands live in HTML pages that update in
        // place, so they need no such workaround.
        await RenderAllAsync(resendPublic: true, resendLog: true);
        RestartTurnTimer();
    }

    #endregion

    #region Round end & scoring

    /// <summary>
    /// Ends the round once at most one player still holds cards: that player takes the last place,
    /// roles and points are handed out, and either the next round starts or the game finishes.
    /// </summary>
    private async Task<bool> TryFinishRoundAsync()
    {
        var stillPlaying = _players.Where(player => player.Hand.Count > 0).ToList();
        if (stillPlaying.Count > 1)
        {
            return false;
        }

        if (stillPlaying.Count == 1)
        {
            var lastPlayer = stillPlaying[0];
            _finishOrder.Add(lastPlayer);
            lastPlayer.FinishPosition = _finishOrder.Count;
        }

        AssignRolesAndPoints();
        LogEvent("president_round_ended", RoundNumber);

        if (RoundNumber >= TotalRounds)
        {
            await FinishGameAsync();
            return true;
        }

        RoundNumber++;
        await BeginRoundAsync();
        return true;
    }

    private void AssignRolesAndPoints()
    {
        for (var position = 1; position <= _finishOrder.Count; position++)
        {
            var player = _finishOrder[position - 1];
            player.Role = RoleForPosition(position, _finishOrder.Count);
            player.Score += _finishOrder.Count - position;

            if (player.Role != PresidentRole.Neutral)
            {
                LogEvent("president_role_earned", player.Name,
                    Context.GetString($"president_role_{player.Role.ToString().ToLowerInvariant()}"));
            }
        }
    }

    private PresidentRole RoleForPosition(int position, int playerCount)
    {
        if (position == 1)
        {
            return PresidentRole.President;
        }

        if (position == playerCount)
        {
            return PresidentRole.Scum;
        }

        if (HasViceRoles && position == 2)
        {
            return PresidentRole.VicePresident;
        }

        if (HasViceRoles && position == playerCount - 1)
        {
            return PresidentRole.ViceScum;
        }

        return PresidentRole.Neutral;
    }

    private async Task FinishGameAsync()
    {
        Phase = PresidentPhase.Finished;
        StopTurnTimer();
        ClearSubPanel();
        await RenderAllAsync();
        OnEnd();
    }

    public async Task ResendPlayerPageAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (Phase == PresidentPhase.Lobby)
            {
                return;
            }

            var player = _players.FirstOrDefault(currentPlayer => currentPlayer.UserId == user.UserId);
            if (player is null)
            {
                return;
            }

            await RenderPlayerPageAsync(player);
        }
        finally
        {
            _actionLock.Release();
        }
    }

    public async Task CancelAsync()
    {
        StopTurnTimer();

        // Cancelled while still gathering players: replace the lobby panel (with its join/start buttons)
        // by a clear "cancelled" notice so the public panel does not look like it is still open.
        if (Phase == PresidentPhase.Lobby)
        {
            await RenderCancelledPublicAsync();
        }

        Phase = PresidentPhase.Finished;
        ClearSubPanel();
        ClearLogPanel();
        OnEnd();
    }

    #endregion

    #region Substitutions

    public async Task<(bool Success, string MessageKey, object[] Args)> RequestSubAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (Phase is PresidentPhase.Lobby or PresidentPhase.Finished)
            {
                return (false, "president_sub_not_active", []);
            }

            var player = _players.FirstOrDefault(currentPlayer => currentPlayer.UserId == user.UserId);
            if (player is null)
            {
                return (false, "president_sub_not_a_player", []);
            }

            // A second request from the same player cancels their pending sub.
            if (player.WantsSub)
            {
                player.WantsSub = false;
                Context.ReplyLocalizedMessage("president_sub_cancelled", player.Name);
                await RenderSubPanelAsync();
                return (true, null, []);
            }

            player.WantsSub = true;
            Context.ReplyLocalizedMessage("president_sub_requested", player.Name);
            // Re-post the panel so a fresh request drops to the bottom of the chat instead of staying
            // stuck high up in the scrollback.
            await RenderSubPanelAsync(forceResend: true);
            return (true, null, []);
        }
        finally
        {
            _actionLock.Release();
        }
    }

    public async Task<(bool Success, string MessageKey, object[] Args)> AcceptSubAsync(IUser user,
        string targetPlayerId)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (Phase is PresidentPhase.Lobby or PresidentPhase.Finished)
            {
                return (false, "president_sub_not_active", []);
            }

            if (_players.Any(currentPlayer => currentPlayer.UserId == user.UserId))
            {
                return (false, "president_sub_already_player", []);
            }

            var pending = _players.Where(currentPlayer => currentPlayer.WantsSub).ToList();
            if (pending.Count == 0)
            {
                return (false, "president_sub_none_pending", []);
            }

            var target = string.IsNullOrWhiteSpace(targetPlayerId)
                ? pending[0]
                : pending.FirstOrDefault(currentPlayer => currentPlayer.UserId == targetPlayerId.ToLowerAlphaNum());
            if (target is null)
            {
                return (false, "president_sub_invalid_target", []);
            }

            var leavingUserId = target.UserId;
            var leavingName = target.Name;
            Context.CloseHtmlPage(leavingUserId, PlayerPageId);
            target.SubstituteWith(user);

            Context.ReplyLocalizedMessage("president_sub_done", user.Name, leavingName);
            await RenderAllAsync();
            await RenderSubPanelAsync();
            return (true, null, []);
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private async Task RenderSubPanelAsync(bool forceResend = false)
    {
        if (_players.All(player => !player.WantsSub))
        {
            ClearSubPanel();
            return;
        }

        if (forceResend)
        {
            ClearSubPanel();
        }

        var html = await _templatesManager.GetTemplateAsync("Games/President/PresidentSub", BuildModel(null));
        Context.SendUpdatableHtml(SubPanelId, html.RemoveNewlines(), isChanging: _subPanelInitialized);
        _subPanelInitialized = true;
    }

    private void ClearSubPanel()
    {
        if (!_subPanelInitialized)
        {
            return;
        }

        Context.SendUpdatableHtml(SubPanelId, string.Empty, isChanging: true);
        _subPanelInitialized = false;
    }

    #endregion

    #region Timeout & action helpers

    private async Task RunActionAsync(Func<Task> action)
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

    private async Task OnTurnTimeoutAsync()
    {
        await _actionLock.WaitAsync();
        try
        {
            switch (Phase)
            {
                case PresidentPhase.Exchange:
                    await AutoGiveAsync();
                    break;
                case PresidentPhase.Playing when CurrentPlayer is not null:
                    if (CurrentTrick.IsEmpty)
                    {
                        var (rank, count) = GetLegalPlays(CurrentPlayer)[0];
                        await PlayCoreAsync(CurrentPlayer.User, rank, count);
                    }
                    else
                    {
                        await PassCoreAsync(CurrentPlayer.User);
                    }

                    break;
            }
        }
        finally
        {
            _actionLock.Release();
        }
    }

    /// <summary>
    /// Resolves the exchange for every player who let their give-back time out, handing over their
    /// lowest cards.
    /// </summary>
    private async Task AutoGiveAsync()
    {
        foreach (var player in _players.Where(currentPlayer => currentPlayer.CardsToGive > 0).ToList())
        {
            var lowestCards = player.Hand.OrderBy(card => card.Rank).Take(player.CardsToGive).ToList();
            await ApplyGiveAsync(player, lowestCards);
        }
    }

    private async Task OnTurnWarningAsync()
    {
        await _actionLock.WaitAsync();
        try
        {
            List<PresidentPlayer> pendingPlayers = Phase switch
            {
                PresidentPhase.Exchange => _players.Where(player => player.CardsToGive > 0).ToList(),
                PresidentPhase.Playing when CurrentPlayer is not null => [CurrentPlayer],
                _ => []
            };

            var seconds = (int)PresidentConstants.TURN_TIMEOUT_WARNING_REMAINING.TotalSeconds;
            var message = Context.GetString("president_turn_timeout_warning", seconds);
            foreach (var player in pendingPlayers)
            {
                Context.SendMessageIn(Context.RoomId, $"/pm {player.UserId}, {message}");
            }
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private void RestartTurnTimer()
    {
        if (Phase is PresidentPhase.Exchange or PresidentPhase.Playing)
        {
            _turnTimer.Restart();
            _turnWarningTimer?.Restart();
        }
    }

    private void StopTurnTimer()
    {
        _turnTimer.Stop();
        _turnWarningTimer?.Stop();
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Appends a localized game event to the running log shown (collapsed) on the public panel and the
    /// player pages.
    /// </summary>
    private void LogEvent(string key, params object[] args)
    {
        _log.Add(Context.GetString(key, args));
    }

    private async Task RenderAllAsync(bool resendPublic = false, bool resendLog = false)
    {
        await RenderPublicAsync(resendPublic);
        await RenderPublicLogAsync(resendLog);
        await RenderPlayerPagesAsync();
    }

    /// <summary>
    /// Renders the running game log into its own updatable chat panel, kept separate from the main table
    /// panel so it only re-renders when a new event has actually been logged. When <paramref name="forceResend"/>
    /// is set (a fresh pile or round), it is re-posted at the bottom of the chat instead of updated in place.
    /// </summary>
    private async Task RenderPublicLogAsync(bool forceResend = false)
    {
        // The final result panel embeds the whole log itself, so drop the live panel once the game ends.
        if (Phase == PresidentPhase.Finished)
        {
            ClearLogPanel();
            return;
        }

        if (_log.Count == 0 || (!forceResend && _logPanelInitialized && _renderedLogCount == _log.Count))
        {
            return;
        }

        var html = await _templatesManager.GetTemplateAsync("Games/President/PresidentLog", BuildModel(null));
        Context.SendUpdatableHtml(LogPanelId, html.RemoveNewlines(), isChanging: _logPanelInitialized && !forceResend);
        _logPanelInitialized = true;
        _renderedLogCount = _log.Count;
    }

    private void ClearLogPanel()
    {
        if (!_logPanelInitialized)
        {
            return;
        }

        Context.SendUpdatableHtml(LogPanelId, string.Empty, isChanging: true);
        _logPanelInitialized = false;
        _renderedLogCount = 0;
    }

    /// <summary>
    /// Renders the public table as a chat panel so spectators (and players) can follow the game from
    /// the room itself. The lobby and the final result are only ever shown here. When
    /// <paramref name="forceResend"/> is set, it is re-posted at the bottom of the chat instead of
    /// updated in place high up in the scrollback.
    /// </summary>
    private async Task RenderPublicAsync(bool forceResend = false)
    {
        var templateKey = Phase switch
        {
            PresidentPhase.Lobby => "Games/President/PresidentLobby",
            PresidentPhase.Finished => "Games/President/PresidentResult",
            _ => "Games/President/PresidentTable"
        };

        var html = await _templatesManager.GetTemplateAsync(templateKey, BuildModel(null));
        Context.SendUpdatableHtml(PublicPanelId, html.RemoveNewlines(),
            isChanging: _publicPanelInitialized && !forceResend);
        _publicPanelInitialized = true;
    }

    private async Task RenderCancelledPublicAsync()
    {
        var html = await _templatesManager.GetTemplateAsync("Games/President/PresidentCancelled", BuildModel(null));
        Context.SendUpdatableHtml(PublicPanelId, html.RemoveNewlines(), _publicPanelInitialized);
        _publicPanelInitialized = true;
    }

    private async Task RenderPlayerPagesAsync()
    {
        foreach (var player in _players)
        {
            await RenderPlayerPageAsync(player);
        }
    }

    /// <summary>
    /// Renders a player's private HTML page: the public table (or result) on top and the player's own
    /// hand with action buttons below. HTML pages update in place, so no chat re-posting is needed.
    /// </summary>
    private async Task RenderPlayerPageAsync(PresidentPlayer player)
    {
        var model = BuildModel(player);

        if (Phase == PresidentPhase.Finished)
        {
            var resultHtml = await _templatesManager.GetTemplateAsync("Games/President/PresidentResult", model);
            Context.SendHtmlPageTo(player.UserId, PlayerPageId, resultHtml.RemoveNewlines());
            return;
        }

        var tableHtml = await _templatesManager.GetTemplateAsync("Games/President/PresidentTable", model);
        var handHtml = await _templatesManager.GetTemplateAsync("Games/President/PresidentHand", model);
        var logHtml = _log.Count > 0
            ? await _templatesManager.GetTemplateAsync("Games/President/PresidentLog", model)
            : string.Empty;
        Context.SendHtmlPageTo(player.UserId, PlayerPageId,
            tableHtml.RemoveNewlines() + logHtml.RemoveNewlines() + handHtml.RemoveNewlines());
    }

    private PresidentViewModel BuildModel(PresidentPlayer viewer) => new()
    {
        Culture = Context.Culture,
        BotName = _configuration.Name,
        Trigger = _configuration.Trigger,
        RoomId = Context.RoomId,
        Game = this,
        Viewer = viewer,
        ViewerHand = viewer?.Hand ?? [],
        ViewerLegalPlays = GetLegalPlays(viewer),
        ViewerCanPass = CanPass(viewer)
    };

    #endregion

    private static void SortHand(List<PresidentCard> hand)
    {
        hand.Sort((first, second) =>
        {
            var rankComparison = first.Rank.CompareTo(second.Rank);
            return rankComparison != 0 ? rankComparison : first.Suit.CompareTo(second.Suit);
        });
    }
}
