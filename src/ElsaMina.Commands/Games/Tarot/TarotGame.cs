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

    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly PeriodicTimerRunner _turnTimer;
    private readonly List<TarotPlayer> _players = [];
    private readonly List<TarotCard> _dog = [];
    private readonly List<TarotCard> _pendingDiscards = [];
    private readonly HashSet<string> _initializedHandPanels = [];

    private int _currentTurnIndex;
    private int _firstLeaderIndex;
    private int _takerIndex = -1;
    private int _partnerIndex = -1;
    private bool _publicPanelInitialized;
    private int _publicPanelSegment;
    private int _handPanelSegment;

    [UsedImplicitly]
    public TarotGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration)
        : this(randomService, templatesManager, configuration, TarotConstants.TURN_TIMEOUT)
    {
    }

    public TarotGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        TimeSpan turnTimeout)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        GameId = Interlocked.Increment(ref _nextGameId);
        _turnTimer = new PeriodicTimerRunner(turnTimeout, OnTurnTimeoutAsync, runOnce: true);
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

    private string PublicPanelId => $"tarot-{GameId}-{_publicPanelSegment}";
    private string HandPanelId(string userId) => $"tarot-hand-{GameId}-{userId}-{_handPanelSegment}";

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

        if (card is null || !card.IsKing)
        {
            Context.ReplyLocalizedMessage("tarot_call_must_be_king");
            return;
        }

        CalledKing = card;
        Context.ReplyLocalizedMessage("tarot_king_called", Taker.Name, card.ToDisplay());

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
                    string.Join(" ", _dog.Select(card => card.ToDisplay())));
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

        await RenderHandsAsync();
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
                string.Join(" ", discardedTrumps.Select(card => card.ToDisplay())));
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
        var (excuseOwner, excusePlayCard) = CurrentTrick.Plays.FirstOrDefault(play => play.Card.IsExcuse);

        foreach (var (_, card) in CurrentTrick.Plays.Where(play => !play.Card.IsExcuse))
        {
            winner.CapturedPile.Add(card);
        }

        if (excusePlayCard is not null)
        {
            excuseOwner.CapturedPile.Add(excusePlayCard);

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

        Context.ReplyLocalizedMessage("tarot_trick_won", winner.Name, TrickNumber);

        LastTrick = CurrentTrick;
        LastTrickWinner = winner;
        _currentTurnIndex = _players.IndexOf(winner);

        if (_players.All(player => player.Hand.Count == 0))
        {
            await FinishAsync();
            return;
        }

        // Force re-post the public and hand panels so they drop back to the bottom of
        // the chat instead of staying stuck high up in the scrollback.
        Context.SendUpdatableHtml(PublicPanelId, string.Empty, true);
        _publicPanelSegment++;
        _publicPanelInitialized = false;

        foreach (var player in _players)
        {
            Context.SendPrivateUpdatableHtml(player.UserId, Context.RoomId, HandPanelId(player.UserId),
                string.Empty, true);
        }
        _handPanelSegment++;
        _initializedHandPanels.Clear();

        TrickNumber++;
        CurrentTrick = new TarotTrick();
        await RenderAllAsync();
        RestartTurnTimer();
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

    #region Scoring & ending

    private async Task FinishAsync()
    {
        var takerSide = _players.Where(player => player.IsTaker || player.IsPartner).ToList();
        var takerHalfPoints = takerSide.Sum(player => player.CapturedPile.Sum(card => card.HalfPoints));
        var oudlerCount = takerSide.Sum(player => player.CapturedPile.Count(card => card.IsOudler));

        ScoreResult = TarotScorer.Compute(takerHalfPoints, oudlerCount, HighestBid,
            _players.Count, _takerIndex, _partnerIndex);

        Phase = TarotPhase.Finished;
        StopTurnTimer();
        await RenderPublicAsync();
        OnEnd();
    }

    public void Cancel()
    {
        StopTurnTimer();
        Phase = TarotPhase.Finished;
        OnEnd();
    }

    private void EndGame()
    {
        StopTurnTimer();
        Phase = TarotPhase.Finished;
        Context.SendUpdatableHtml(PublicPanelId, string.Empty, true);
        OnEnd();
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
        var kings = new[] { TarotSuit.Hearts, TarotSuit.Spades, TarotSuit.Diamonds, TarotSuit.Clubs }
            .Select(suit => new TarotCard(suit, TarotCard.KING))
            .ToList();
        return kings.FirstOrDefault(king => !Taker.Hand.Contains(king)) ?? kings[0];
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

    private void RestartTurnTimer()
    {
        if (Phase is TarotPhase.Bidding or TarotPhase.KingCall or TarotPhase.Discard or TarotPhase.Playing)
        {
            _turnTimer.Restart();
        }
    }

    private void StopTurnTimer() => _turnTimer.Stop();

    #endregion

    #region Rendering

    private async Task RenderAllAsync()
    {
        await RenderPublicAsync();
        await RenderHandsAsync();
    }

    private async Task RenderPublicAsync()
    {
        var templateKey = Phase switch
        {
            TarotPhase.Lobby => "Games/Tarot/TarotLobby",
            TarotPhase.Finished => "Games/Tarot/TarotResult",
            _ => "Games/Tarot/TarotTable"
        };

        var html = await _templatesManager.GetTemplateAsync(templateKey, BuildModel(null));
        Context.SendUpdatableHtml(PublicPanelId, html.RemoveNewlines(), _publicPanelInitialized);
        _publicPanelInitialized = true;
    }

    private async Task RenderHandsAsync()
    {
        foreach (var player in _players)
        {
            var model = BuildModel(player);
            var html = await _templatesManager.GetTemplateAsync("Games/Tarot/TarotHand", model);
            var alreadyInitialized = _initializedHandPanels.Contains(player.UserId);
            Context.SendPrivateUpdatableHtml(player.UserId, Context.RoomId, HandPanelId(player.UserId),
                html.RemoveNewlines(), alreadyInitialized);
            _initializedHandPanels.Add(player.UserId);
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