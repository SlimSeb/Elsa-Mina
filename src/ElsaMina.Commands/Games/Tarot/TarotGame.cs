using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.Tarot;

public class TarotGame : Game, ITarotGame
{
    private static int _nextGameId;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly ITarotStatsService _statsService;

    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly PeriodicTimerRunner _turnTimer;
    private readonly PeriodicTimerRunner _turnWarningTimer;
    private readonly List<TarotPlayer> _players = [];
    private readonly List<TarotCard> _dog = [];
    private readonly List<TarotCard> _pendingDiscards = [];
    private readonly List<(TarotPlayer Player, int Tier)> _declaredPoignees = [];

    private int _currentTurnIndex;
    private int _firstLeaderIndex;
    private int _takerIndex = -1;
    private int _partnerIndex = -1;
    private int _takerSideTrickWins;
    private int _cardsPlayedTotal;
    private bool _slamAnnounced;
    private bool _publicPanelInitialized;
    private bool _subPanelInitialized;

    [UsedImplicitly]
    public TarotGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        ITarotStatsService statsService)
        : this(randomService, templatesManager, configuration, statsService, TarotConstants.TURN_TIMEOUT)
    {
    }

    public TarotGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        ITarotStatsService statsService, TimeSpan turnTimeout)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _statsService = statsService;
        GameId = Interlocked.Increment(ref _nextGameId);
        _turnTimer = new PeriodicTimerRunner(turnTimeout, OnTurnTimeoutAsync, runOnce: true);

        // Warn the active player by PM once only the warning threshold of time is left on their turn.
        var warningDelay = turnTimeout - TarotConstants.TURN_TIMEOUT_WARNING_REMAINING;
        if (warningDelay > TimeSpan.Zero)
        {
            _turnWarningTimer = new PeriodicTimerRunner(warningDelay, OnTurnWarningAsync, runOnce: true);
        }
    }

    public int GameId { get; }
    public override string Identifier => nameof(TarotGame);

    public IContext Context { get; set; }

    public IReadOnlyList<TarotPlayer> Players => _players;
    public int PlayerCount => _players.Count;
    public TarotPhase Phase { get; private set; } = TarotPhase.Lobby;

    public TarotPlayer CurrentPlayer =>
        _currentTurnIndex >= 0 && _currentTurnIndex < _players.Count ? _players[_currentTurnIndex] : null;

    public TarotPlayer Taker => _takerIndex >= 0 ? _players[_takerIndex] : null;
    public TarotBid HighestBid { get; private set; } = TarotBid.Pass;

    public IReadOnlyList<TarotCard> Dog => _dog;
    public IReadOnlyList<TarotCard> PendingDiscards => _pendingDiscards;
    public bool DogRevealed { get; private set; }
    public TarotCard CalledKing { get; private set; }
    public TarotPlayer Partner => _partnerIndex >= 0 ? _players[_partnerIndex] : null;
    public bool PartnerRevealed { get; private set; }

    public TarotTrick CurrentTrick { get; private set; } = new();
    public TarotTrick LastTrick { get; private set; }
    public TarotPlayer LastTrickWinner { get; private set; }
    public TarotCard LastPlayedCard => CurrentTrick.Plays.Count > 0 ? CurrentTrick.Plays[^1].Card : null;
    public int TrickNumber { get; private set; }
    public int TotalTricks => _players.Count > 0 ? TarotConstants.HAND_SIZE[_players.Count] : 0;

    public TarotScoreResult ScoreResult { get; private set; }

    public bool SlamAnnounced => _slamAnnounced;
    public IReadOnlyList<(TarotPlayer Player, int Tier)> DeclaredPoignees => _declaredPoignees;

    private string PublicPanelId => $"tarot-{GameId}";
    private string PlayerPageId => $"tarot-{GameId}";
    private string SubPanelId => $"tarot-{GameId}-sub";

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
            if (Phase != TarotPhase.Lobby)
            {
                return (false, "tarot_join_already_started", []);
            }

            if (_players.Count >= TarotConstants.MAX_PLAYERS)
            {
                return (false, "tarot_join_full", []);
            }

            if (_players.Any(player => player.UserId == user.UserId))
            {
                return (false, "tarot_join_already_joined", []);
            }

            _players.Add(new TarotPlayer(user));
            await RenderPublicAsync();
            return (true, "tarot_join_success", [user.Name]);
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
            if (Phase != TarotPhase.Lobby)
            {
                Context.ReplyLocalizedMessage("tarot_start_already_started");
                return;
            }

            if (_players.Count < TarotConstants.MIN_PLAYERS)
            {
                Context.ReplyLocalizedMessage("tarot_start_not_enough_players", TarotConstants.MIN_PLAYERS);
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

        var deck = TarotConstants.BuildDeck();
        _randomService.ShuffleInPlace(deck);

        var handSize = TarotConstants.HAND_SIZE[_players.Count];
        var dogSize = TarotConstants.DOG_SIZE[_players.Count];

        var cursor = 0;
        foreach (var player in _players)
        {
            for (var card = 0; card < handSize; card++)
            {
                player.Hand.Add(deck[cursor++]);
            }

            SortHand(player.Hand);
        }

        for (var card = 0; card < dogSize; card++)
        {
            _dog.Add(deck[cursor++]);
        }

        Phase = TarotPhase.Bidding;
        _firstLeaderIndex = 0;
        _currentTurnIndex = 0;

        await RenderAllAsync();
        RestartTurnTimer();
    }

    public Task BidAsync(IUser user, TarotBid bid) => RunActionAsync(() => BidCoreAsync(user, bid));

    private async Task BidCoreAsync(IUser user, TarotBid bid)
    {
        if (Phase != TarotPhase.Bidding || CurrentPlayer?.UserId != user.UserId)
        {
            return;
        }

        if (bid != TarotBid.Pass && bid <= HighestBid)
        {
            Context.ReplyLocalizedMessage("tarot_bid_too_low", user.Name);
            return;
        }

        var player = CurrentPlayer;
        player.HasBid = true;
        player.Bid = bid;
        if (bid > HighestBid)
        {
            HighestBid = bid;
        }

        if (_players.All(currentPlayer => currentPlayer.HasBid))
        {
            await ResolveBiddingAsync();
            return;
        }

        do
        {
            _currentTurnIndex = (_currentTurnIndex + 1) % _players.Count;
        } while (CurrentPlayer.HasBid);

        await RenderAllAsync();
        RestartTurnTimer();
    }

    private async Task ResolveBiddingAsync()
    {
        if (HighestBid == TarotBid.Pass)
        {
            Context.ReplyLocalizedMessage("tarot_bidding_all_passed");
            EndGame();
            return;
        }

        _takerIndex = _players.FindIndex(player => player.HasBid && player.Bid == HighestBid);
        _players[_takerIndex].IsTaker = true;
        _currentTurnIndex = _takerIndex;

        Context.ReplyLocalizedMessage("tarot_taker_announced", Taker.Name, GetBidName(HighestBid));

        if (_players.Count == 5)
        {
            Phase = TarotPhase.KingCall;
            await RenderAllAsync();
            RestartTurnTimer();
            return;
        }

        await ResolveDogAsync();
    }

    #endregion

    #region King call (5 players)

    public Task CallKingAsync(IUser user, TarotCard card) => RunActionAsync(() => CallKingCoreAsync(user, card));

    private async Task CallKingCoreAsync(IUser user, TarotCard card)
    {
        if (Phase != TarotPhase.KingCall || Taker?.UserId != user.UserId)
        {
            return;
        }

        // A taker holding all four kings must instead call a queen to find their partner.
        var mustCallQueen = TakerHoldsAllKings();
        if (card is null || (mustCallQueen ? !card.IsQueen : !card.IsKing))
        {
            Context.ReplyLocalizedMessage(mustCallQueen ? "tarot_call_must_be_queen" : "tarot_call_must_be_king");
            return;
        }

        CalledKing = card;
        Context.ReplyLocalizedMessage("tarot_king_called", Taker.Name, card.ToDisplay(Context.Culture));

        await ResolveDogAsync();
    }

    #endregion

    #region Dog handling

    private async Task ResolveDogAsync()
    {
        switch (HighestBid)
        {
            case TarotBid.Petite or TarotBid.Garde:
                DogRevealed = true;
                Context.ReplyLocalizedMessage("tarot_dog_revealed",
                    string.Join(" ", _dog.Select(card => card.ToDisplay(Context.Culture))));
                Taker.Hand.AddRange(_dog);
                SortHand(Taker.Hand);
                Phase = TarotPhase.Discard;
                _currentTurnIndex = _takerIndex;
                await RenderAllAsync();
                RestartTurnTimer();
                return;

            case TarotBid.GardeSans:
                Taker.CapturedPile.AddRange(_dog);
                await BeginPlayAsync();
                return;

            case TarotBid.GardeContre:
                // Dog stays apart and counts for the defenders: it is left out of every captured pile.
                await BeginPlayAsync();
                return;
        }
    }

    public Task DiscardAsync(IUser user, IReadOnlyList<TarotCard> cards) =>
        RunActionAsync(() => DiscardCoreAsync(user, cards));

    private async Task DiscardCoreAsync(IUser user, IReadOnlyList<TarotCard> cards)
    {
        if (Phase != TarotPhase.Discard || Taker?.UserId != user.UserId || cards is null || cards.Count == 0)
        {
            return;
        }

        var dogSize = TarotConstants.DOG_SIZE[_players.Count];

        // A full list (e.g. typed in one go) is applied directly; anything else toggles the selection.
        if (cards.Count == dogSize && cards.Distinct().Count() == dogSize)
        {
            await ApplyDiscardAsync(cards, dogSize);
            return;
        }

        foreach (var card in cards)
        {
            if (!Taker.Hand.Contains(card))
            {
                Context.ReplyLocalizedMessage("tarot_discard_not_in_hand");
                return;
            }

            if (card.IsKing || card.IsOudler)
            {
                Context.ReplyLocalizedMessage("tarot_discard_forbidden_card");
                return;
            }

            if (!_pendingDiscards.Remove(card) && _pendingDiscards.Count < dogSize)
            {
                _pendingDiscards.Add(card);
            }
        }

        if (_pendingDiscards.Count == dogSize)
        {
            await ApplyDiscardAsync(_pendingDiscards.ToList(), dogSize);
            return;
        }

        await RenderPlayerPagesAsync();
        RestartTurnTimer();
    }

    private async Task ApplyDiscardAsync(IReadOnlyList<TarotCard> cards, int dogSize)
    {
        if (cards.Count != dogSize
            || cards.Distinct().Count() != cards.Count
            || cards.Any(card => !Taker.Hand.Contains(card)))
        {
            Context.ReplyLocalizedMessage("tarot_discard_not_in_hand");
            return;
        }

        if (cards.Any(card => card.IsKing || card.IsOudler))
        {
            Context.ReplyLocalizedMessage("tarot_discard_forbidden_card");
            return;
        }

        // Trumps may only be buried when there are not enough other cards to fill the dog, and even
        // then only the minimum number that is forced.
        var freelyDiscardable = Taker.Hand.Count(card => !card.IsKing && !card.IsOudler && !card.IsTrump);
        var allowedTrumps = Math.Max(0, dogSize - freelyDiscardable);
        if (cards.Count(card => card.IsTrump) > allowedTrumps)
        {
            Context.ReplyLocalizedMessage("tarot_discard_trump_not_allowed");
            return;
        }

        foreach (var card in cards)
        {
            Taker.Hand.Remove(card);
            Taker.CapturedPile.Add(card);
        }

        _pendingDiscards.Clear();

        var discardedTrumps = cards.Where(card => card.IsTrump).ToList();
        if (discardedTrumps.Count > 0)
        {
            Context.ReplyLocalizedMessage("tarot_discard_trumps_revealed",
                string.Join(" ", discardedTrumps.Select(card => card.ToDisplay(Context.Culture))));
        }

        await BeginPlayAsync();
    }

    #endregion

    #region Playing

    private async Task BeginPlayAsync()
    {
        if (_players.Count == 5 && CalledKing is not null)
        {
            DeterminePartner();
        }

        Phase = TarotPhase.Playing;
        TrickNumber = 1;
        _takerSideTrickWins = 0;
        _cardsPlayedTotal = 0;
        CurrentTrick = new TarotTrick();
        _currentTurnIndex = _firstLeaderIndex;

        await RenderAllAsync();
        RestartTurnTimer();
    }

    private void DeterminePartner()
    {
        var holderIndex = _players.FindIndex(player => player.Hand.Contains(CalledKing));
        if (holderIndex >= 0 && holderIndex != _takerIndex)
        {
            _partnerIndex = holderIndex;
            _players[holderIndex].IsPartner = true;
        }
    }

    public Task PlayAsync(IUser user, TarotCard card) => RunActionAsync(() => PlayCoreAsync(user, card));

    private async Task PlayCoreAsync(IUser user, TarotCard card)
    {
        if (Phase != TarotPhase.Playing || CurrentPlayer?.UserId != user.UserId)
        {
            return;
        }

        var player = CurrentPlayer;
        if (card is null || !player.Hand.Contains(card))
        {
            Context.ReplyLocalizedMessage("tarot_play_not_in_hand");
            return;
        }

        if (!GetLegalMoves(player).Contains(card))
        {
            Context.ReplyLocalizedMessage("tarot_play_illegal");
            return;
        }

        player.Hand.Remove(card);
        CurrentTrick.Add(player, card);
        player.HasPlayed = true;
        _cardsPlayedTotal++;

        if (CalledKing is not null && card == CalledKing && player.IsPartner)
        {
            PartnerRevealed = true;
            Context.ReplyLocalizedMessage("tarot_partner_revealed", player.Name);
        }

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
        var winnerIsTakerSide = winner.IsTaker || winner.IsPartner;
        var isLastTrick = _players.All(player => player.Hand.Count == 0);
        var (excuseOwner, excusePlayCard) = CurrentTrick.Plays.FirstOrDefault(play => play.Card.IsExcuse);

        foreach (var (_, card) in CurrentTrick.Plays.Where(play => !play.Card.IsExcuse))
        {
            winner.CapturedPile.Add(card);
        }

        if (excusePlayCard is not null)
        {
            HandleExcuseCapture(excuseOwner, excusePlayCard, winner, winnerIsTakerSide, isLastTrick);
        }

        if (winnerIsTakerSide)
        {
            _takerSideTrickWins++;
        }

        Context.ReplyLocalizedMessage("tarot_trick_won", winner.Name, TrickNumber);

        LastTrick = CurrentTrick;
        LastTrickWinner = winner;
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
        CurrentTrick = new TarotTrick();
        await RenderAllAsync();
        RestartTurnTimer();
    }

    /// <summary>
    /// Assigns the Excuse once a trick is resolved. On ordinary tricks the owner keeps it and pays a
    /// low card to the trick winner. On the last trick the Excuse goes to the trick winner instead,
    /// unless the side that played it has just made a slam (then it stays, and wins).
    /// </summary>
    private void HandleExcuseCapture(TarotPlayer excuseOwner, TarotCard excuseCard, TarotPlayer winner,
        bool winnerIsTakerSide, bool isLastTrick)
    {
        if (isLastTrick)
        {
            var takerSideWins = _takerSideTrickWins + (winnerIsTakerSide ? 1 : 0);
            var excuseOwnerIsTakerSide = excuseOwner.IsTaker || excuseOwner.IsPartner;
            var ownerSlam = excuseOwnerIsTakerSide ? takerSideWins == TotalTricks : takerSideWins == 0;

            (ownerSlam ? excuseOwner : winner).CapturedPile.Add(excuseCard);
            return;
        }

        excuseOwner.CapturedPile.Add(excuseCard);

        if (excuseOwner != winner)
        {
            var lowCard = excuseOwner.CapturedPile.FirstOrDefault(card => !card.IsExcuse && card.HalfPoints == 1);
            if (lowCard is not null)
            {
                excuseOwner.CapturedPile.Remove(lowCard);
                winner.CapturedPile.Add(lowCard);
            }
        }
    }

    /// <summary>
    /// The cards the given player may legally play to the current trick.
    /// </summary>
    public IReadOnlyCollection<TarotCard> GetLegalMoves(TarotPlayer player)
    {
        var hand = player.Hand;
        var excuse = hand.FirstOrDefault(card => card.IsExcuse);

        // Leading, or only the Excuse has been played so far: anything goes.
        if (CurrentTrick.IsEmpty || CurrentTrick.LeadSuit is null)
        {
            return hand.ToList();
        }

        var legal = new List<TarotCard>();
        var leadSuit = CurrentTrick.LeadSuit.Value;
        var highestTrump = CurrentTrick.HighestTrumpRank;
        var trumps = hand.Where(card => card.IsTrump).ToList();

        if (leadSuit == TarotSuit.Trump)
        {
            AddTrumpMoves(legal, trumps, highestTrump, hand);
        }
        else
        {
            var suitCards = hand.Where(card => card.Suit == leadSuit).ToList();
            if (suitCards.Count > 0)
            {
                legal.AddRange(suitCards);
            }
            else
            {
                AddTrumpMoves(legal, trumps, highestTrump, hand);
            }
        }

        if (excuse is not null && !legal.Contains(excuse))
        {
            legal.Add(excuse);
        }

        return legal;
    }

    private static void AddTrumpMoves(List<TarotCard> legal, List<TarotCard> trumps, int? highestTrump,
        List<TarotCard> hand)
    {
        if (trumps.Count == 0)
        {
            legal.AddRange(hand.Where(card => !card.IsExcuse));
            return;
        }

        var overtrumps = trumps.Where(card => highestTrump is null || card.Rank > highestTrump).ToList();
        legal.AddRange(overtrumps.Count > 0 ? overtrumps : trumps);
    }

    #endregion

    #region Declarations (poignée & chelem)

    /// <summary>
    /// The poignée tier (1 single, 2 double, 3 triple, 0 none) the player could declare with their
    /// current hand. The Excuse may stand in for a missing trump to reach a tier.
    /// </summary>
    public int GetDeclarablePoigneeTier(TarotPlayer player)
    {
        if (player is null || _players.Count == 0)
        {
            return 0;
        }

        var thresholds = TarotConstants.POIGNEE_THRESHOLDS[_players.Count];
        var trumpCount = player.Hand.Count(card => card.IsTrump);
        var hasExcuse = player.Hand.Any(card => card.IsExcuse);

        var tier = TierForTrumpCount(trumpCount, thresholds);
        if (hasExcuse)
        {
            tier = Math.Max(tier, TierForTrumpCount(trumpCount + 1, thresholds));
        }

        return tier;
    }

    private static int TierForTrumpCount(int count, IReadOnlyList<int> thresholds)
    {
        if (count >= thresholds[2])
        {
            return 3;
        }

        if (count >= thresholds[1])
        {
            return 2;
        }

        return count >= thresholds[0] ? 1 : 0;
    }

    public bool CanDeclarePoignee(TarotPlayer player) =>
        Phase == TarotPhase.Playing && player is { HasPlayed: false, HasDeclaredPoignee: false }
        && GetDeclarablePoigneeTier(player) > 0;

    public bool CanAnnounceSlam(TarotPlayer player) =>
        Phase == TarotPhase.Playing && _cardsPlayedTotal == 0 && !_slamAnnounced && player is { IsTaker: true };

    public Task DeclarePoigneeAsync(IUser user) => RunActionAsync(() => DeclarePoigneeCoreAsync(user));

    private async Task DeclarePoigneeCoreAsync(IUser user)
    {
        if (Phase != TarotPhase.Playing)
        {
            return;
        }

        var player = _players.FirstOrDefault(currentPlayer => currentPlayer.UserId == user.UserId);
        if (player is null || player.HasPlayed || player.HasDeclaredPoignee)
        {
            return;
        }

        var tier = GetDeclarablePoigneeTier(player);
        if (tier == 0)
        {
            Context.ReplyLocalizedMessage("tarot_poignee_not_enough");
            return;
        }

        player.HasDeclaredPoignee = true;
        player.PoigneeTier = tier;
        _declaredPoignees.Add((player, tier));

        var trumps = player.Hand
            .Where(card => card.IsTrump || card.IsExcuse)
            .OrderBy(card => card.IsExcuse ? 0 : card.Rank)
            .Select(card => card.ToDisplay(Context.Culture));
        Context.ReplyLocalizedMessage("tarot_poignee_declared", player.Name,
            Context.GetString($"tarot_poignee_tier_{tier}"), string.Join(" ", trumps));

        await RenderAllAsync();
    }

    public Task AnnounceSlamAsync(IUser user) => RunActionAsync(() => AnnounceSlamCoreAsync(user));

    private async Task AnnounceSlamCoreAsync(IUser user)
    {
        if (Phase != TarotPhase.Playing || _cardsPlayedTotal > 0 || _slamAnnounced)
        {
            return;
        }

        if (Taker?.UserId != user.UserId)
        {
            Context.ReplyLocalizedMessage("tarot_slam_taker_only");
            return;
        }

        _slamAnnounced = true;
        Context.ReplyLocalizedMessage("tarot_slam_announced", Taker.Name);
        await RenderAllAsync();
    }

    private int ComputePetitAuBoutSide()
    {
        if (LastTrick is null || LastTrickWinner is null)
        {
            return 0;
        }

        var petitInLastTrick = LastTrick.Plays
            .Any(play => play.Card.IsTrump && play.Card.Rank == TarotCard.PETIT);
        if (!petitInLastTrick)
        {
            return 0;
        }

        return LastTrickWinner.IsTaker || LastTrickWinner.IsPartner ? 1 : -1;
    }

    private bool TakerHoldsAllKings() =>
        Taker is not null && new[] { TarotSuit.Hearts, TarotSuit.Spades, TarotSuit.Diamonds, TarotSuit.Clubs }
            .All(suit => Taker.Hand.Contains(new TarotCard(suit, TarotCard.KING)));

    #endregion

    #region Scoring & ending

    private async Task FinishAsync()
    {
        var takerSide = _players.Where(player => player.IsTaker || player.IsPartner).ToList();
        var takerHalfPoints = takerSide.Sum(player => player.CapturedPile.Sum(card => card.HalfPoints));
        var oudlerCount = takerSide.Sum(player => player.CapturedPile.Count(card => card.IsOudler));

        var petitAuBoutSide = ComputePetitAuBoutSide();
        var poigneeHalfPoints = _declaredPoignees.Sum(declaration => TarotConstants.POIGNEE_HALF_POINTS[declaration.Tier]);
        var slamWinnerSide = _takerSideTrickWins == TotalTricks ? 1 : _takerSideTrickWins == 0 ? -1 : 0;

        ScoreResult = TarotScorer.Compute(takerHalfPoints, oudlerCount, HighestBid,
            _players.Count, _takerIndex, _partnerIndex,
            petitAuBoutSide, poigneeHalfPoints, slamWinnerSide, _slamAnnounced);

        Phase = TarotPhase.Finished;
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
            if (Phase == TarotPhase.Lobby)
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
        if (Phase == TarotPhase.Lobby)
        {
            await RenderCancelledPublicAsync();
        }

        Phase = TarotPhase.Finished;
        ClearSubPanel();
        OnEnd();
    }

    private void EndGame()
    {
        StopTurnTimer();
        Phase = TarotPhase.Finished;
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
            if (Phase is TarotPhase.Lobby or TarotPhase.Finished)
            {
                return (false, "tarot_sub_not_active", []);
            }

            var player = _players.FirstOrDefault(currentPlayer => currentPlayer.UserId == user.UserId);
            if (player is null)
            {
                return (false, "tarot_sub_not_a_player", []);
            }

            // A second request from the same player cancels their pending sub.
            if (player.WantsSub)
            {
                player.WantsSub = false;
                Context.ReplyLocalizedMessage("tarot_sub_cancelled", player.Name);
                await RenderSubPanelAsync();
                return (true, null, []);
            }

            player.WantsSub = true;
            Context.ReplyLocalizedMessage("tarot_sub_requested", player.Name);
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
            if (Phase is TarotPhase.Lobby or TarotPhase.Finished)
            {
                return (false, "tarot_sub_not_active", []);
            }

            if (_players.Any(currentPlayer => currentPlayer.UserId == user.UserId))
            {
                return (false, "tarot_sub_already_player", []);
            }

            var pending = _players.Where(currentPlayer => currentPlayer.WantsSub).ToList();
            if (pending.Count == 0)
            {
                return (false, "tarot_sub_none_pending", []);
            }

            var target = string.IsNullOrWhiteSpace(targetPlayerId)
                ? pending[0]
                : pending.FirstOrDefault(currentPlayer => currentPlayer.UserId == targetPlayerId.ToLowerAlphaNum());
            if (target is null)
            {
                return (false, "tarot_sub_invalid_target", []);
            }

            var leavingUserId = target.UserId;
            var leavingName = target.Name;
            Context.CloseHtmlPage(leavingUserId, PlayerPageId);
            target.SubstituteWith(user);

            Context.ReplyLocalizedMessage("tarot_sub_done", user.Name, leavingName);
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

        var html = await _templatesManager.GetTemplateAsync("Games/Tarot/TarotSub", BuildModel(null));
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
                case TarotPhase.Bidding:
                    await BidCoreAsync(CurrentPlayer.User, TarotBid.Pass);
                    break;
                case TarotPhase.KingCall:
                    await CallKingCoreAsync(Taker.User, ChooseAutoKing());
                    break;
                case TarotPhase.Discard:
                    await DiscardCoreAsync(Taker.User, ChooseAutoDiscards());
                    break;
                case TarotPhase.Playing:
                    await PlayCoreAsync(CurrentPlayer.User, GetLegalMoves(CurrentPlayer).First());
                    break;
            }
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private TarotCard ChooseAutoKing()
    {
        var suits = new[] { TarotSuit.Hearts, TarotSuit.Spades, TarotSuit.Diamonds, TarotSuit.Clubs };

        // With all four kings in hand, a queen must be called instead. Otherwise call a king the taker
        // does not hold, so a partner is found.
        var rank = TakerHoldsAllKings() ? TarotCard.QUEEN : TarotCard.KING;
        var candidates = suits.Select(suit => new TarotCard(suit, rank)).ToList();
        return candidates.FirstOrDefault(card => !Taker.Hand.Contains(card)) ?? candidates[0];
    }

    private List<TarotCard> ChooseAutoDiscards()
    {
        var dogSize = TarotConstants.DOG_SIZE[_players.Count];
        var discardable = Taker.Hand
            .Where(card => !card.IsKing && !card.IsOudler && !card.IsTrump)
            .OrderBy(card => card.HalfPoints)
            .ToList();

        if (discardable.Count < dogSize)
        {
            discardable.AddRange(Taker.Hand
                .Where(card => card.IsTrump && !card.IsOudler)
                .OrderBy(card => card.Rank)
                .Take(dogSize - discardable.Count));
        }

        return discardable.Take(dogSize).ToList();
    }

    private async Task OnTurnWarningAsync()
    {
        await _actionLock.WaitAsync();
        try
        {
            var player = Phase switch
            {
                TarotPhase.KingCall or TarotPhase.Discard => Taker,
                TarotPhase.Bidding or TarotPhase.Playing => CurrentPlayer,
                _ => null
            };

            if (player is null)
            {
                return;
            }

            var seconds = (int)TarotConstants.TURN_TIMEOUT_WARNING_REMAINING.TotalSeconds;
            var message = Context.GetString("tarot_turn_timeout_warning", seconds);
            Context.SendMessageIn(Context.RoomId, $"/pm {player.UserId}, {message}");
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private void RestartTurnTimer()
    {
        if (Phase is TarotPhase.Bidding or TarotPhase.KingCall or TarotPhase.Discard or TarotPhase.Playing)
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

    /// <summary>
    /// Renders the public table as a chat panel so spectators (and players) can follow the game from
    /// the room itself. The lobby and the final result are only ever shown here.
    /// </summary>
    private async Task RenderPublicAsync(bool forceResend = false)
    {
        var templateKey = Phase switch
        {
            TarotPhase.Lobby => "Games/Tarot/TarotLobby",
            TarotPhase.Finished => "Games/Tarot/TarotResult",
            _ => "Games/Tarot/TarotTable"
        };

        var html = await _templatesManager.GetTemplateAsync(templateKey, BuildModel(null));
        Context.SendUpdatableHtml(PublicPanelId, html.RemoveNewlines(), forceResend || _publicPanelInitialized);
        _publicPanelInitialized = true;
    }

    private async Task RenderCancelledPublicAsync()
    {
        var html = await _templatesManager.GetTemplateAsync("Games/Tarot/TarotCancelled", BuildModel(null));
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
    private async Task RenderPlayerPageAsync(TarotPlayer player)
    {
        var model = BuildModel(player);

        if (Phase == TarotPhase.Finished)
        {
            var resultHtml = await _templatesManager.GetTemplateAsync("Games/Tarot/TarotResult", model);
            Context.SendHtmlPageTo(player.UserId, PlayerPageId, resultHtml.RemoveNewlines());
            return;
        }

        var tableHtml = await _templatesManager.GetTemplateAsync("Games/Tarot/TarotTable", model);
        var handHtml = await _templatesManager.GetTemplateAsync("Games/Tarot/TarotHand", model);
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

    private TarotViewModel BuildModel(TarotPlayer viewer) => new()
    {
        Culture = Context.Culture,
        BotName = _configuration.Name,
        Trigger = _configuration.Trigger,
        RoomId = Context.RoomId,
        Game = this,
        Viewer = viewer,
        ViewerHand = viewer?.Hand ?? [],
        ViewerLegalMoves = viewer is not null && Phase == TarotPhase.Playing && CurrentPlayer == viewer
            ? GetLegalMoves(viewer)
            : []
    };

    #endregion

    private static void SortHand(List<TarotCard> hand)
    {
        hand.Sort((first, second) =>
        {
            var suitComparison = first.Suit.CompareTo(second.Suit);
            return suitComparison != 0 ? suitComparison : first.Rank.CompareTo(second.Rank);
        });
    }

    private string GetBidName(TarotBid bid) => Context.GetString($"tarot_bid_{bid.ToString().ToLowerInvariant()}");
}