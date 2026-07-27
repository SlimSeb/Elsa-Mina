using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.Tarot;

public class TarotGame : SubstitutableCardGame<TarotPlayer>, ITarotGame
{
    private readonly IRandomService _randomService;
    private readonly IConfiguration _configuration;
    private readonly ITarotStatsService _statsService;

    private readonly List<TarotCard> _dog = [];
    private readonly List<TarotCard> _pendingDiscards = [];
    private readonly List<(TarotPlayer Player, int Tier)> _declaredPoignees = [];
    private readonly List<(TarotPlayer Player, TarotMisereType Type)> _declaredMiseres = [];

    private int _firstLeaderIndex;
    private int _takerIndex = -1;
    private int _partnerIndex = -1;
    private int _takerSideTrickWins;
    private int _cardsPlayedTotal;
    private bool _slamAnnounced;

    [UsedImplicitly]
    public TarotGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        ITarotStatsService statsService)
        : this(randomService, templatesManager, configuration, statsService, TarotConstants.TURN_TIMEOUT)
    {
    }

    public TarotGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        ITarotStatsService statsService, TimeSpan turnTimeout)
        : base(templatesManager, turnTimeout, TarotConstants.TURN_TIMEOUT_WARNING_REMAINING)
    {
        _randomService = randomService;
        _configuration = configuration;
        _statsService = statsService;
    }

    public override string Identifier => nameof(TarotGame);

    public TarotPhase Phase { get; private set; } = TarotPhase.Lobby;

    public override bool IsInLobby => Phase == TarotPhase.Lobby;

    protected override string ResourcePrefix => "tarot";
    protected override string TemplateFolder => "Tarot";
    protected override int MinPlayers => TarotConstants.MIN_PLAYERS;
    protected override int MaxPlayers => TarotConstants.MAX_PLAYERS;
    protected override bool IsFinished => Phase == TarotPhase.Finished;

    protected override bool IsAcceptingActions =>
        Phase is TarotPhase.Bidding or TarotPhase.KingCall or TarotPhase.Discard or TarotPhase.Playing;

    protected override TarotPlayer CreatePlayer(IUser user) => new(user);

    protected override void MarkFinished() => Phase = TarotPhase.Finished;

    public Task<(bool Success, string MessageKey, object[] Args)> LeaveAsync(IUser user) => LeaveSeatAsync(user);

    public TarotPlayer CurrentPlayer => CurrentSeat;

    public TarotPlayer Taker => _takerIndex >= 0 ? Seats[_takerIndex] : null;
    public TarotBid HighestBid { get; private set; } = TarotBid.Pass;

    public IReadOnlyList<TarotCard> Dog => _dog;
    public IReadOnlyList<TarotCard> PendingDiscards => _pendingDiscards;
    public bool DogRevealed { get; private set; }
    public TarotCard CalledKing { get; private set; }
    public TarotPlayer Partner => _partnerIndex >= 0 ? Seats[_partnerIndex] : null;
    public bool PartnerRevealed { get; private set; }

    public TarotTrick CurrentTrick { get; private set; } = new();
    public TarotTrick LastTrick { get; private set; }
    public TarotPlayer LastTrickWinner { get; private set; }
    public TarotCard LastPlayedCard => CurrentTrick.Plays.Count > 0 ? CurrentTrick.Plays[^1].Card : null;
    public int TrickNumber { get; private set; }
    public int TotalTricks => Seats.Count > 0 ? TarotConstants.HAND_SIZE[Seats.Count] : 0;

    public TarotScoreResult ScoreResult { get; private set; }

    public bool SlamAnnounced => _slamAnnounced;
    public IReadOnlyList<(TarotPlayer Player, int Tier)> DeclaredPoignees => _declaredPoignees;
    public IReadOnlyList<(TarotPlayer Player, TarotMisereType Type)> DeclaredMiseres => _declaredMiseres;

    #region Dealing & bidding

    protected override async Task StartDealAsync()
    {
        await RenderPublicAsync();

        OnStart();

        _randomService.ShuffleInPlace(Seats);

        await DealNewHandAsync();
    }

    /// <summary>
    /// Resets all per-deal state and deals a fresh hand, opening a new bidding phase. Used both for the
    /// first deal and to redeal (keeping the same seating) when every player passes.
    /// </summary>
    private async Task DealNewHandAsync()
    {
        ResetDealState();

        var deck = TarotConstants.BuildDeck();
        _randomService.ShuffleInPlace(deck);

        var handSize = TarotConstants.HAND_SIZE[Seats.Count];
        var dogSize = TarotConstants.DOG_SIZE[Seats.Count];

        var cursor = 0;
        foreach (var player in Seats)
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
        CurrentTurnIndex = 0;

        // Re-post the public chat panel and the log panel so each fresh deal (the first one and any redeal
        // after every player passed) drops back to the bottom of the chat instead of staying in scrollback.
        await RenderAllAsync(resendPublic: true, resendLog: true);
        RestartTurnTimer();
    }

    /// <summary>
    /// Clears every piece of per-deal state so a fresh hand can be dealt over the same set of players.
    /// </summary>
    private void ResetDealState()
    {
        foreach (var player in Seats)
        {
            player.Hand.Clear();
            player.CapturedPile.Clear();
            player.Bid = TarotBid.Pass;
            player.HasBid = false;
            player.IsTaker = false;
            player.IsPartner = false;
            player.HasPlayed = false;
            player.HasDeclaredPoignee = false;
            player.PoigneeTier = 0;
            player.HasDeclaredMisere = false;
            player.DeclaredMisereTypes.Clear();
        }

        _dog.Clear();
        _pendingDiscards.Clear();
        _declaredPoignees.Clear();
        _declaredMiseres.Clear();

        HighestBid = TarotBid.Pass;
        _takerIndex = -1;
        _partnerIndex = -1;
        _takerSideTrickWins = 0;
        _cardsPlayedTotal = 0;
        _slamAnnounced = false;

        CalledKing = null;
        DogRevealed = false;
        PartnerRevealed = false;

        CurrentTrick = new TarotTrick();
        LastTrick = null;
        LastTrickWinner = null;
        TrickNumber = 0;
        ScoreResult = null;
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

        if (Seats.All(currentPlayer => currentPlayer.HasBid))
        {
            await ResolveBiddingAsync();
            return;
        }

        do
        {
            AdvanceTurn();
        } while (CurrentPlayer.HasBid);

        await RenderAllAsync();
        RestartTurnTimer();
    }

    private async Task ResolveBiddingAsync()
    {
        if (HighestBid == TarotBid.Pass)
        {
            LogEvent("tarot_bidding_all_passed");
            await DealNewHandAsync();
            return;
        }

        _takerIndex = Seats.FindIndex(player => player.HasBid && player.Bid == HighestBid);
        Seats[_takerIndex].IsTaker = true;
        CurrentTurnIndex = _takerIndex;

        LogEvent("tarot_taker_announced", Taker.Name, GetBidName(HighestBid));

        if (Seats.Count == 5)
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
        LogEvent("tarot_king_called", Taker.Name, card.ToDisplay(Context.Culture));

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
                LogEvent("tarot_dog_revealed",
                    string.Join(" ", _dog.Select(card => card.ToDisplay(Context.Culture))));
                Taker.Hand.AddRange(_dog);
                SortHand(Taker.Hand);
                Phase = TarotPhase.Discard;
                CurrentTurnIndex = _takerIndex;
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

        var dogSize = TarotConstants.DOG_SIZE[Seats.Count];

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
            LogEvent("tarot_discard_trumps_revealed",
                string.Join(" ", discardedTrumps.Select(card => card.ToDisplay(Context.Culture))));
        }

        await BeginPlayAsync();
    }

    #endregion

    #region Playing

    private async Task BeginPlayAsync()
    {
        if (Seats.Count == 5 && CalledKing is not null)
        {
            DeterminePartner();
        }

        Phase = TarotPhase.Playing;
        TrickNumber = 1;
        _takerSideTrickWins = 0;
        _cardsPlayedTotal = 0;
        CurrentTrick = new TarotTrick();
        CurrentTurnIndex = _firstLeaderIndex;

        await RenderAllAsync();
        RestartTurnTimer();
    }

    private void DeterminePartner()
    {
        var holderIndex = Seats.FindIndex(player => player.Hand.Contains(CalledKing));
        if (holderIndex >= 0 && holderIndex != _takerIndex)
        {
            _partnerIndex = holderIndex;
            Seats[holderIndex].IsPartner = true;
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
            LogEvent("tarot_partner_revealed", player.Name);
        }

        if (CurrentTrick.Plays.Count < Seats.Count)
        {
            AdvanceTurn();
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
        var isLastTrick = Seats.All(player => player.Hand.Count == 0);
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

        LogEvent("tarot_trick_won", winner.Name, TrickNumber);

        LastTrick = CurrentTrick;
        LastTrickWinner = winner;
        CurrentTurnIndex = SeatIndexOf(winner);

        if (Seats.All(player => player.Hand.Count == 0))
        {
            await FinishAsync();
            return;
        }

        // Force re-post the public chat panel so it drops back to the bottom of the chat instead of
        // staying stuck high up in the scrollback. Player hands and tables live in HTML pages that
        // update in place, so they need no such workaround.
        TrickNumber++;
        CurrentTrick = new TarotTrick();
        await RenderAllAsync(resendPublic: true, resendLog: true);
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
    public IReadOnlyCollection<TarotCard> GetLegalMoves(TarotPlayer player) =>
        TarotRules.GetLegalMoves(player.Hand, CurrentTrick, Seats.Count, CalledKing, TrickNumber);

    #endregion

    #region Declarations (poignée & chelem)

    /// <summary>
    /// The poignée tier (1 single, 2 double, 3 triple, 0 none) the player could declare with their
    /// current hand. The Excuse may stand in for a missing trump to reach a tier.
    /// </summary>
    public int GetDeclarablePoigneeTier(TarotPlayer player) =>
        player is null || Seats.Count == 0
            ? 0
            : TarotRules.GetDeclarablePoigneeTier(player.Hand, Seats.Count);

    public bool CanDeclarePoignee(TarotPlayer player) =>
        Phase == TarotPhase.Playing && player is { HasPlayed: false, HasDeclaredPoignee: false }
                                    && GetDeclarablePoigneeTier(player) > 0;

    public bool CanAnnounceSlam(TarotPlayer player) =>
        Phase == TarotPhase.Playing && _cardsPlayedTotal == 0 && !_slamAnnounced && player is { IsTaker: true };

    /// <summary>
    /// The misère types the player could declare with their current hand: a misère d'atout when they
    /// hold no trump (the Excuse is tolerated), a misère de tête when they hold no face card.
    /// </summary>
    public IReadOnlyList<TarotMisereType> GetDeclarableMisereTypes(TarotPlayer player) =>
        player is null || player.Hand.Count == 0 ? [] : TarotRules.GetDeclarableMisereTypes(player.Hand);

    public bool CanDeclareMisere(TarotPlayer player) =>
        Phase == TarotPhase.Playing && player is { HasPlayed: false, HasDeclaredMisere: false }
                                    && GetDeclarableMisereTypes(player).Count > 0;

    public Task DeclarePoigneeAsync(IUser user) => RunActionAsync(() => DeclarePoigneeCoreAsync(user));

    private async Task DeclarePoigneeCoreAsync(IUser user)
    {
        if (Phase != TarotPhase.Playing)
        {
            return;
        }

        var player = FindSeat(user.UserId);
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
        LogEvent("tarot_poignee_declared", player.Name,
            Context.GetString($"tarot_poignee_tier_{tier}"), string.Join(" ", trumps));

        await RenderAllAsync();
    }

    public Task DeclareMisereAsync(IUser user) => RunActionAsync(() => DeclareMisereCoreAsync(user));

    private async Task DeclareMisereCoreAsync(IUser user)
    {
        if (Phase != TarotPhase.Playing)
        {
            return;
        }

        var player = FindSeat(user.UserId);
        if (player is null || player.HasPlayed || player.HasDeclaredMisere)
        {
            return;
        }

        var types = GetDeclarableMisereTypes(player);
        if (types.Count == 0)
        {
            Context.ReplyLocalizedMessage("tarot_misere_none");
            return;
        }

        player.HasDeclaredMisere = true;
        player.DeclaredMisereTypes.AddRange(types);
        foreach (var type in types)
        {
            _declaredMiseres.Add((player, type));
        }

        var typeNames =
            types.Select(type => Context.GetString($"tarot_misere_type_{type.ToString().ToLowerInvariant()}"));
        LogEvent("tarot_misere_declared", player.Name, string.Join(", ", typeNames));

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
        LogEvent("tarot_slam_announced", Taker.Name);
        await RenderAllAsync();
    }

    private bool TakerHoldsAllKings() => Taker is not null && TarotRules.HoldsAllKings(Taker.Hand);

    #endregion

    #region Scoring & ending

    private async Task FinishAsync()
    {
        var takerSide = Seats.Where(player => player.IsTaker || player.IsPartner).ToList();
        var takerHalfPoints = takerSide.Sum(player => player.CapturedPile.Sum(card => card.HalfPoints));
        var oudlerCount = takerSide.Sum(player => player.CapturedPile.Count(card => card.IsOudler));

        var petitAuBoutSide = TarotRules.ComputePetitAuBoutSide(LastTrick, LastTrickWinner);
        var poigneeHalfPoints =
            _declaredPoignees.Sum(declaration => TarotConstants.POIGNEE_HALF_POINTS[declaration.Tier]);
        var slamWinnerSide = _takerSideTrickWins == TotalTricks ? 1 : _takerSideTrickWins == 0 ? -1 : 0;

        var miserePlayerHalfPoints = new int[Seats.Count];
        foreach (var (player, _) in _declaredMiseres)
        {
            miserePlayerHalfPoints[SeatIndexOf(player)] += TarotConstants.MISERE_HALF_POINTS;
        }

        ScoreResult = TarotScorer.Compute(takerHalfPoints, oudlerCount, HighestBid,
            Seats.Count, _takerIndex, _partnerIndex,
            petitAuBoutSide, poigneeHalfPoints, slamWinnerSide, _slamAnnounced,
            miserePlayerHalfPoints);

        Phase = TarotPhase.Finished;
        StopTurnTimer();
        ClearSubPanel();
        await _statsService.RecordDealAsync(Seats, ScoreResult);
        await RenderAllAsync();
        OnEnd();
    }

    #endregion

    #region Timeouts

    protected override async Task OnTurnTimeoutAsync()
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

    protected override IEnumerable<TarotPlayer> GetTurnWarningRecipients()
    {
        var player = Phase switch
        {
            TarotPhase.KingCall or TarotPhase.Discard => Taker,
            TarotPhase.Bidding or TarotPhase.Playing => CurrentPlayer,
            _ => null
        };

        return player is null ? [] : [player];
    }

    private TarotCard ChooseAutoKing()
    {
        // With all four kings in hand, a queen must be called instead. Otherwise call a king the taker
        // does not hold, so a partner is found.
        var rank = TakerHoldsAllKings() ? TarotCard.QUEEN : TarotCard.KING;
        var candidates = TarotConstants.Suits.Select(suit => TarotCard.Suited(suit, rank)).ToList();
        return candidates.FirstOrDefault(card => !Taker.Hand.Contains(card)) ?? candidates[0];
    }

    private List<TarotCard> ChooseAutoDiscards()
    {
        var dogSize = TarotConstants.DOG_SIZE[Seats.Count];
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

    #endregion

    protected override TarotViewModel BuildModel(TarotPlayer viewer) => new()
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

    /// <summary>
    /// Orders a hand the way it is displayed: the four suits first, then the trumps, then the Excuse,
    /// each group ordered by rank.
    /// </summary>
    private static void SortHand(List<TarotCard> hand)
    {
        hand.Sort((first, second) =>
        {
            var kindComparison = first.Kind.CompareTo(second.Kind);
            if (kindComparison != 0)
            {
                return kindComparison;
            }

            var suitComparison = Nullable.Compare(first.Suit, second.Suit);
            return suitComparison != 0 ? suitComparison : first.Rank.CompareTo(second.Rank);
        });
    }

    private string GetBidName(TarotBid bid) => Context.GetString($"tarot_bid_{bid.ToString().ToLowerInvariant()}");
}
