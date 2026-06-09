using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.Belote;

public class BeloteGame : Game, IBeloteGame
{
    private static int _nextGameId;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IBeloteStatsService _statsService;

    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly PeriodicTimerRunner _turnTimer;
    private readonly PeriodicTimerRunner _turnWarningTimer;
    private readonly List<BelotePlayer> _players = [];

    private List<BeloteCard> _deck = [];
    private int _dealCursor;

    private int _currentTurnIndex;
    private int _firstLeaderIndex;
    private int _takerIndex = -1;
    private int _bidsThisRound;
    private int _team0Tricks;
    private int _team1Tricks;
    private int _lastTrickTeam = -1;
    private bool _publicPanelInitialized;
    private bool _subPanelInitialized;

    [UsedImplicitly]
    public BeloteGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        IBeloteStatsService statsService)
        : this(randomService, templatesManager, configuration, statsService, BeloteConstants.TURN_TIMEOUT)
    {
    }

    public BeloteGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        IBeloteStatsService statsService, TimeSpan turnTimeout)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _statsService = statsService;
        GameId = Interlocked.Increment(ref _nextGameId);
        _turnTimer = new PeriodicTimerRunner(turnTimeout, OnTurnTimeoutAsync, runOnce: true);

        // Warn the active player by PM once only the warning threshold of time is left on their turn.
        var warningDelay = turnTimeout - BeloteConstants.TURN_TIMEOUT_WARNING_REMAINING;
        if (warningDelay > TimeSpan.Zero)
        {
            _turnWarningTimer = new PeriodicTimerRunner(warningDelay, OnTurnWarningAsync, runOnce: true);
        }
    }

    public int GameId { get; }
    public override string Identifier => nameof(BeloteGame);

    public IContext Context { get; set; }

    public IReadOnlyList<BelotePlayer> Players => _players;
    public int PlayerCount => _players.Count;
    public BelotePhase Phase { get; private set; } = BelotePhase.Lobby;
    public int BiddingRound { get; private set; }

    public BelotePlayer CurrentPlayer =>
        _currentTurnIndex >= 0 && _currentTurnIndex < _players.Count ? _players[_currentTurnIndex] : null;

    public BelotePlayer Taker => _takerIndex >= 0 ? _players[_takerIndex] : null;
    public BeloteCard TurnedCard { get; private set; }
    public BeloteSuit? Trump { get; private set; }

    public BeloteTrick CurrentTrick { get; private set; }
    public BeloteTrick LastTrick { get; private set; }
    public BelotePlayer LastTrickWinner { get; private set; }
    public BeloteCard LastPlayedCard => CurrentTrick is { Plays.Count: > 0 } ? CurrentTrick.Plays[^1].Card : null;
    public int TrickNumber { get; private set; }
    public int TotalTricks => BeloteConstants.TRICK_COUNT;

    public int Team0Tricks => _team0Tricks;
    public int Team1Tricks => _team1Tricks;

    public BeloteScoreResult ScoreResult { get; private set; }

    private string PublicPanelId => $"belote-{GameId}";
    private string PlayerPageId => $"belote-{GameId}";
    private string SubPanelId => $"belote-{GameId}-sub";

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
            if (Phase != BelotePhase.Lobby)
            {
                return (false, "belote_join_already_started", []);
            }

            if (_players.Count >= BeloteConstants.PLAYER_COUNT)
            {
                return (false, "belote_join_full", []);
            }

            if (_players.Any(player => player.UserId == user.UserId))
            {
                return (false, "belote_join_already_joined", []);
            }

            _players.Add(new BelotePlayer(user));
            await RenderPublicAsync();
            return (true, "belote_join_success", [user.Name]);
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
            if (Phase != BelotePhase.Lobby)
            {
                Context.ReplyLocalizedMessage("belote_start_already_started");
                return;
            }

            if (_players.Count != BeloteConstants.PLAYER_COUNT)
            {
                Context.ReplyLocalizedMessage("belote_start_not_enough_players", BeloteConstants.PLAYER_COUNT);
                return;
            }

            await RenderPublicAsync(forceResend: true);
            await DealAndStartBiddingAsync();
        }
        finally
        {
            _actionLock.Release();
        }
    }

    #endregion

    #region Dealing & bidding

    private async Task DealAndStartBiddingAsync()
    {
        OnStart();

        _randomService.ShuffleInPlace(_players);
        for (var seat = 0; seat < _players.Count; seat++)
        {
            _players[seat].Team = seat % 2;
        }

        _deck = BeloteConstants.BuildDeck();
        _randomService.ShuffleInPlace(_deck);

        _dealCursor = 0;
        foreach (var player in _players)
        {
            for (var card = 0; card < 5; card++)
            {
                player.Hand.Add(_deck[_dealCursor++]);
            }

            SortHand(player.Hand, null);
        }

        TurnedCard = _deck[_dealCursor++];

        Phase = BelotePhase.Bidding;
        BiddingRound = 1;
        _bidsThisRound = 0;
        _firstLeaderIndex = 0;
        _currentTurnIndex = 0;

        await RenderAllAsync();
        RestartTurnTimer();
    }

    public Task BidAsync(IUser user, bool pass, BeloteSuit? chosenSuit) =>
        RunActionAsync(() => BidCoreAsync(user, pass, chosenSuit));

    private async Task BidCoreAsync(IUser user, bool pass, BeloteSuit? chosenSuit)
    {
        if (Phase != BelotePhase.Bidding || CurrentPlayer?.UserId != user.UserId)
        {
            return;
        }

        if (!pass)
        {
            BeloteSuit trump;
            if (BiddingRound == 1)
            {
                trump = TurnedCard.Suit;
            }
            else
            {
                if (chosenSuit is null)
                {
                    Context.ReplyLocalizedMessage("belote_bid_choose_suit");
                    return;
                }

                if (chosenSuit.Value == TurnedCard.Suit)
                {
                    Context.ReplyLocalizedMessage("belote_bid_suit_forbidden");
                    return;
                }

                trump = chosenSuit.Value;
            }

            _takerIndex = _currentTurnIndex;
            CurrentPlayer.IsTaker = true;
            Context.ReplyLocalizedMessage("belote_taker_announced", Taker.Name, GetSuitName(trump),
                BeloteCard.SuitDisplay(trump));

            await BeginPlayAsync(trump);
            return;
        }

        CurrentPlayer.HasBid = true;
        _bidsThisRound++;

        if (_bidsThisRound >= BeloteConstants.PLAYER_COUNT)
        {
            if (BiddingRound == 1)
            {
                StartSecondBiddingRound();
                await RenderAllAsync();
                RestartTurnTimer();
                return;
            }

            Context.ReplyLocalizedMessage("belote_bidding_all_passed");
            EndGame();
            return;
        }

        _currentTurnIndex = (_currentTurnIndex + 1) % _players.Count;
        await RenderAllAsync();
        RestartTurnTimer();
    }

    private void StartSecondBiddingRound()
    {
        BiddingRound = 2;
        _bidsThisRound = 0;
        _currentTurnIndex = 0;
        foreach (var player in _players)
        {
            player.HasBid = false;
        }
    }

    #endregion

    #region Playing

    private async Task BeginPlayAsync(BeloteSuit trump)
    {
        Trump = trump;

        // The taker takes the turned card, then the rest of the deck is dealt out so that everyone
        // holds eight cards (the taker needs two more, the others three each).
        Taker.Hand.Add(TurnedCard);
        foreach (var player in _players)
        {
            var count = player.IsTaker ? 2 : 3;
            for (var card = 0; card < count; card++)
            {
                player.Hand.Add(_deck[_dealCursor++]);
            }

            SortHand(player.Hand, trump);
        }

        DetectBelote(trump);

        Phase = BelotePhase.Playing;
        TrickNumber = 1;
        CurrentTrick = new BeloteTrick(trump);
        _currentTurnIndex = _firstLeaderIndex;

        await RenderAllAsync();
        RestartTurnTimer();
    }

    private void DetectBelote(BeloteSuit trump)
    {
        var king = new BeloteCard(trump, BeloteCard.KING);
        var queen = new BeloteCard(trump, BeloteCard.QUEEN);
        foreach (var player in _players)
        {
            if (player.Hand.Contains(king) && player.Hand.Contains(queen))
            {
                player.HasBelote = true;
            }
        }
    }

    public Task PlayAsync(IUser user, BeloteCard card) => RunActionAsync(() => PlayCoreAsync(user, card));

    private async Task PlayCoreAsync(IUser user, BeloteCard card)
    {
        if (Phase != BelotePhase.Playing || CurrentPlayer?.UserId != user.UserId)
        {
            return;
        }

        var player = CurrentPlayer;
        if (card is null || !player.Hand.Contains(card))
        {
            Context.ReplyLocalizedMessage("belote_play_not_in_hand");
            return;
        }

        if (!GetLegalMoves(player).Contains(card))
        {
            Context.ReplyLocalizedMessage("belote_play_illegal");
            return;
        }

        player.Hand.Remove(card);
        CurrentTrick.Add(player, card);

        if (CurrentTrick.Plays.Count < _players.Count)
        {
            _currentTurnIndex = (_currentTurnIndex + 1) % _players.Count;
            await RenderAllAsync();
            RestartTurnTimer();
            return;
        }

        await ResolveTrickAsync();
    }

    private async Task ResolveTrickAsync()
    {
        var winner = CurrentTrick.DetermineWinner();

        foreach (var (_, card) in CurrentTrick.Plays)
        {
            winner.CapturedPile.Add(card);
        }

        if (winner.Team == 0)
        {
            _team0Tricks++;
        }
        else
        {
            _team1Tricks++;
        }

        Context.ReplyLocalizedMessage("belote_trick_won", winner.Name, TrickNumber);

        LastTrick = CurrentTrick;
        LastTrickWinner = winner;
        _lastTrickTeam = winner.Team;
        _currentTurnIndex = _players.IndexOf(winner);

        if (_players.All(player => player.Hand.Count == 0))
        {
            await FinishAsync();
            return;
        }

        // Force re-post the public chat panel so it drops back to the bottom of the chat instead of
        // staying stuck high up in the scrollback. Player hands and tables live in HTML pages that
        // update in place, so they need no such workaround.
        Context.SendUpdatableHtml(PublicPanelId, string.Empty, true);
        _publicPanelInitialized = false;

        TrickNumber++;
        CurrentTrick = new BeloteTrick(Trump!.Value);
        await RenderAllAsync();
        RestartTurnTimer();
    }

    /// <summary>
    /// The cards the given player may legally play to the current trick, following the Belote rules:
    /// follow suit if possible; when following trump, over-trump if able; when unable to follow, trump
    /// (and over-trump if a trump is already down) unless the partner is already winning the trick.
    /// </summary>
    public IReadOnlyCollection<BeloteCard> GetLegalMoves(BelotePlayer player)
    {
        var hand = player.Hand;
        if (CurrentTrick is null || CurrentTrick.IsEmpty || Trump is null)
        {
            return hand.ToList();
        }

        var trump = Trump.Value;
        var leadSuit = CurrentTrick.LeadSuit!.Value;
        var handTrumps = hand.Where(card => card.IsTrump(trump)).ToList();
        var highestTrumpStrength = CurrentTrick.HighestTrumpStrength;

        if (leadSuit == trump)
        {
            if (handTrumps.Count == 0)
            {
                return hand.ToList();
            }

            var overTrumps = handTrumps
                .Where(card => highestTrumpStrength is null || card.GetStrength(trump) > highestTrumpStrength)
                .ToList();
            return overTrumps.Count > 0 ? overTrumps : handTrumps;
        }

        var handLead = hand.Where(card => card.Suit == leadSuit).ToList();
        if (handLead.Count > 0)
        {
            return handLead;
        }

        var winner = CurrentTrick.CurrentWinner;
        if (winner is not null && winner.Team == player.Team)
        {
            // The partner is master of the trick: the player is free to discard anything.
            return hand.ToList();
        }

        if (handTrumps.Count == 0)
        {
            return hand.ToList();
        }

        var overCuts = handTrumps
            .Where(card => highestTrumpStrength is null || card.GetStrength(trump) > highestTrumpStrength)
            .ToList();
        return overCuts.Count > 0 ? overCuts : handTrumps;
    }

    #endregion

    #region Scoring & ending

    private async Task FinishAsync()
    {
        var trump = Trump!.Value;
        var team0CardPoints = _players
            .Where(player => player.Team == 0)
            .Sum(player => player.CapturedPile.Sum(card => card.GetPoints(trump)));
        var team1CardPoints = _players
            .Where(player => player.Team == 1)
            .Sum(player => player.CapturedPile.Sum(card => card.GetPoints(trump)));
        var beloteTeam = _players.FirstOrDefault(player => player.HasBelote)?.Team ?? -1;

        ScoreResult = BeloteScorer.Compute(Taker.Team, team0CardPoints, team1CardPoints, _lastTrickTeam,
            _team0Tricks, _team1Tricks, beloteTeam, _players);

        Phase = BelotePhase.Finished;
        StopTurnTimer();
        ClearSubPanel();
        await _statsService.RecordDealAsync(_players, ScoreResult);
        await RenderAllAsync();
        OnEnd();
    }

    public async Task ResendPlayerPageAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (Phase == BelotePhase.Lobby)
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

    public void Cancel()
    {
        StopTurnTimer();
        Phase = BelotePhase.Finished;
        ClearSubPanel();
        OnEnd();
    }

    private void EndGame()
    {
        StopTurnTimer();
        Phase = BelotePhase.Finished;
        Context.SendUpdatableHtml(PublicPanelId, string.Empty, true);
        ClearSubPanel();
        ClosePlayerPages();
        OnEnd();
    }

    #endregion

    #region Substitutions

    public async Task<(bool Success, string MessageKey, object[] Args)> RequestSubAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (Phase is BelotePhase.Lobby or BelotePhase.Finished)
            {
                return (false, "belote_sub_not_active", []);
            }

            var player = _players.FirstOrDefault(currentPlayer => currentPlayer.UserId == user.UserId);
            if (player is null)
            {
                return (false, "belote_sub_not_a_player", []);
            }

            // A second request from the same player cancels their pending sub.
            if (player.WantsSub)
            {
                player.WantsSub = false;
                Context.ReplyLocalizedMessage("belote_sub_cancelled", player.Name);
                await RenderSubPanelAsync();
                return (true, null, []);
            }

            player.WantsSub = true;
            Context.ReplyLocalizedMessage("belote_sub_requested", player.Name);
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

    public async Task<(bool Success, string MessageKey, object[] Args)> AcceptSubAsync(IUser user, string targetPlayerId)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (Phase is BelotePhase.Lobby or BelotePhase.Finished)
            {
                return (false, "belote_sub_not_active", []);
            }

            if (_players.Any(currentPlayer => currentPlayer.UserId == user.UserId))
            {
                return (false, "belote_sub_already_player", []);
            }

            var pending = _players.Where(currentPlayer => currentPlayer.WantsSub).ToList();
            if (pending.Count == 0)
            {
                return (false, "belote_sub_none_pending", []);
            }

            var target = string.IsNullOrWhiteSpace(targetPlayerId)
                ? pending[0]
                : pending.FirstOrDefault(currentPlayer => currentPlayer.UserId == targetPlayerId.ToLowerAlphaNum());
            if (target is null)
            {
                return (false, "belote_sub_invalid_target", []);
            }

            var leavingUserId = target.UserId;
            var leavingName = target.Name;
            Context.CloseHtmlPage(leavingUserId, PlayerPageId);
            target.SubstituteWith(user);

            Context.ReplyLocalizedMessage("belote_sub_done", user.Name, leavingName);
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

        var html = await _templatesManager.GetTemplateAsync("Games/Belote/BeloteSub", BuildModel(null));
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
                case BelotePhase.Bidding:
                    await BidCoreAsync(CurrentPlayer.User, pass: true, null);
                    break;
                case BelotePhase.Playing:
                    await PlayCoreAsync(CurrentPlayer.User, GetLegalMoves(CurrentPlayer).First());
                    break;
            }
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private async Task OnTurnWarningAsync()
    {
        await _actionLock.WaitAsync();
        try
        {
            var player = Phase is BelotePhase.Bidding or BelotePhase.Playing ? CurrentPlayer : null;
            if (player is null)
            {
                return;
            }

            var seconds = (int)BeloteConstants.TURN_TIMEOUT_WARNING_REMAINING.TotalSeconds;
            var message = Context.GetString("belote_turn_timeout_warning", seconds);
            Context.SendMessageIn(Context.RoomId, $"/pm {player.UserId}, {message}");
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private void RestartTurnTimer()
    {
        if (Phase is BelotePhase.Bidding or BelotePhase.Playing)
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

    private async Task RenderAllAsync()
    {
        await RenderPublicAsync();
        await RenderPlayerPagesAsync();
    }

    private async Task RenderPublicAsync(bool forceResend = false)
    {
        var templateKey = Phase switch
        {
            BelotePhase.Lobby => "Games/Belote/BeloteLobby",
            BelotePhase.Finished => "Games/Belote/BeloteResult",
            _ => "Games/Belote/BeloteTable"
        };

        var html = await _templatesManager.GetTemplateAsync(templateKey, BuildModel(null));
        Context.SendUpdatableHtml(PublicPanelId, html.RemoveNewlines(), forceResend || _publicPanelInitialized);
        _publicPanelInitialized = true;
    }

    private async Task RenderPlayerPagesAsync()
    {
        foreach (var player in _players)
        {
            await RenderPlayerPageAsync(player);
        }
    }

    private async Task RenderPlayerPageAsync(BelotePlayer player)
    {
        var model = BuildModel(player);

        if (Phase == BelotePhase.Finished)
        {
            var resultHtml = await _templatesManager.GetTemplateAsync("Games/Belote/BeloteResult", model);
            Context.SendHtmlPageTo(player.UserId, PlayerPageId, resultHtml.RemoveNewlines());
            return;
        }

        var tableHtml = await _templatesManager.GetTemplateAsync("Games/Belote/BeloteTable", model);
        var handHtml = await _templatesManager.GetTemplateAsync("Games/Belote/BeloteHand", model);
        Context.SendHtmlPageTo(player.UserId, PlayerPageId,
            tableHtml.RemoveNewlines() + handHtml.RemoveNewlines());
    }

    private void ClosePlayerPages()
    {
        foreach (var player in _players)
        {
            Context.CloseHtmlPage(player.UserId, PlayerPageId);
        }
    }

    private BeloteViewModel BuildModel(BelotePlayer viewer) => new()
    {
        Culture = Context.Culture,
        BotName = _configuration.Name,
        Trigger = _configuration.Trigger,
        RoomId = Context.RoomId,
        Game = this,
        Viewer = viewer,
        ViewerHand = viewer?.Hand ?? [],
        ViewerLegalMoves = viewer is not null && Phase == BelotePhase.Playing && CurrentPlayer == viewer
            ? GetLegalMoves(viewer)
            : []
    };

    #endregion

    private void SortHand(List<BeloteCard> hand, BeloteSuit? trump)
    {
        hand.Sort((first, second) =>
        {
            var suitComparison = first.Suit.CompareTo(second.Suit);
            if (suitComparison != 0)
            {
                return suitComparison;
            }

            // Within a suit, order by in-game strength so the strongest cards sit on the right.
            if (trump is not null)
            {
                return first.GetStrength(trump.Value).CompareTo(second.GetStrength(trump.Value));
            }

            return first.Rank.CompareTo(second.Rank);
        });
    }

    private string GetSuitName(BeloteSuit suit) =>
        Context.GetString($"belote_suit_{suit.ToString().ToLowerInvariant()}");
}
