using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.Belote;

public class BeloteGame : SubstitutableCardGame<BelotePlayer>, IBeloteGame
{
    private readonly IRandomService _randomService;
    private readonly IConfiguration _configuration;
    private readonly IBeloteStatsService _statsService;

    private List<BeloteCard> _deck = [];
    private int _dealCursor;

    private int _firstLeaderIndex;
    private int _takerIndex = -1;
    private int _bidsThisRound;
    private int _team0Tricks;
    private int _team1Tricks;
    private int _lastTrickTeam = -1;

    [UsedImplicitly]
    public BeloteGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        IBeloteStatsService statsService)
        : this(randomService, templatesManager, configuration, statsService, BeloteConstants.TURN_TIMEOUT)
    {
    }

    public BeloteGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        IBeloteStatsService statsService, TimeSpan turnTimeout)
        : base(templatesManager, turnTimeout, BeloteConstants.TURN_TIMEOUT_WARNING_REMAINING)
    {
        _randomService = randomService;
        _configuration = configuration;
        _statsService = statsService;
    }

    public override string Identifier => nameof(BeloteGame);

    public BelotePhase Phase { get; private set; } = BelotePhase.Lobby;
    public int BiddingRound { get; private set; }

    public override bool IsInLobby => Phase == BelotePhase.Lobby;

    protected override string ResourcePrefix => "belote";
    protected override string TemplateFolder => "Belote";
    protected override int MinPlayers => BeloteConstants.PLAYER_COUNT;
    protected override int MaxPlayers => BeloteConstants.PLAYER_COUNT;
    protected override bool IsFinished => Phase == BelotePhase.Finished;
    protected override bool IsAcceptingActions => Phase is BelotePhase.Bidding or BelotePhase.Playing;

    protected override BelotePlayer CreatePlayer(IUser user) => new(user);

    protected override void MarkFinished() => Phase = BelotePhase.Finished;

    public BelotePlayer CurrentPlayer => CurrentSeat;

    public BelotePlayer Taker => _takerIndex >= 0 ? Seats[_takerIndex] : null;
    public BeloteCard TurnedCard { get; private set; }
    public Suit? Trump { get; private set; }

    public BeloteTrick CurrentTrick { get; private set; }
    public BeloteTrick LastTrick { get; private set; }
    public BelotePlayer LastTrickWinner { get; private set; }
    public BeloteCard LastPlayedCard => CurrentTrick is { Plays.Count: > 0 } ? CurrentTrick.Plays[^1].Card : null;
    public int TrickNumber { get; private set; }
    public int TotalTricks => BeloteConstants.TRICK_COUNT;

    public int Team0Tricks => _team0Tricks;
    public int Team1Tricks => _team1Tricks;

    public BeloteScoreResult ScoreResult { get; private set; }

    #region Dealing & bidding

    protected override async Task StartDealAsync()
    {
        // Drop the lobby panel and let the deal post a fresh one at the bottom of the chat, the same way
        // every resolved trick does, instead of updating it in place high up in the scrollback.
        WipePublicPanel();

        OnStart();

        _randomService.ShuffleInPlace(Seats);
        for (var seat = 0; seat < Seats.Count; seat++)
        {
            Seats[seat].Team = seat % 2;
        }

        _deck = BeloteConstants.BuildDeck();
        _randomService.ShuffleInPlace(_deck);

        _dealCursor = 0;
        foreach (var player in Seats)
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
        CurrentTurnIndex = 0;

        await RenderAllAsync();
        RestartTurnTimer();
    }

    public Task BidAsync(IUser user, bool pass, Suit? chosenSuit) =>
        RunActionAsync(() => BidCoreAsync(user, pass, chosenSuit));

    private async Task BidCoreAsync(IUser user, bool pass, Suit? chosenSuit)
    {
        if (Phase != BelotePhase.Bidding || CurrentPlayer?.UserId != user.UserId)
        {
            return;
        }

        if (!pass)
        {
            Suit trump;
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

            _takerIndex = CurrentTurnIndex;
            CurrentPlayer.IsTaker = true;
            Context.ReplyLocalizedMessage("belote_taker_announced", Taker.Name, GetSuitName(trump),
                CardToken.SuitSymbol(trump));

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

        AdvanceTurn();
        await RenderAllAsync();
        RestartTurnTimer();
    }

    private void StartSecondBiddingRound()
    {
        BiddingRound = 2;
        _bidsThisRound = 0;
        CurrentTurnIndex = 0;
        foreach (var player in Seats)
        {
            player.HasBid = false;
        }
    }

    #endregion

    #region Playing

    private async Task BeginPlayAsync(Suit trump)
    {
        Trump = trump;

        // The taker takes the turned card, then the rest of the deck is dealt out so that everyone
        // holds eight cards (the taker needs two more, the others three each).
        Taker.Hand.Add(TurnedCard);
        foreach (var player in Seats)
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
        CurrentTurnIndex = _firstLeaderIndex;

        await RenderAllAsync();
        RestartTurnTimer();
    }

    private void DetectBelote(Suit trump)
    {
        var king = new BeloteCard(trump, BeloteCard.KING);
        var queen = new BeloteCard(trump, BeloteCard.QUEEN);
        foreach (var player in Seats)
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

        LogEvent("belote_trick_won", winner.Name, TrickNumber);

        LastTrick = CurrentTrick;
        LastTrickWinner = winner;
        _lastTrickTeam = winner.Team;
        CurrentTurnIndex = SeatIndexOf(winner);

        if (Seats.All(player => player.Hand.Count == 0))
        {
            await FinishAsync();
            return;
        }

        // Force re-post the public chat panel and its log so they drop back to the bottom of the chat
        // instead of staying stuck high up in the scrollback. Player hands and tables live in HTML pages
        // that update in place, so they need no such workaround.
        WipePublicPanel();

        TrickNumber++;
        CurrentTrick = new BeloteTrick(Trump!.Value);
        await RenderAllAsync(resendLog: true);
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
        var team0CardPoints = Seats
            .Where(player => player.Team == 0)
            .Sum(player => player.CapturedPile.Sum(card => card.GetPoints(trump)));
        var team1CardPoints = Seats
            .Where(player => player.Team == 1)
            .Sum(player => player.CapturedPile.Sum(card => card.GetPoints(trump)));
        var beloteTeam = Seats.FirstOrDefault(player => player.HasBelote)?.Team ?? -1;

        ScoreResult = BeloteScorer.Compute(Taker.Team, team0CardPoints, team1CardPoints, _lastTrickTeam,
            _team0Tricks, _team1Tricks, beloteTeam, Seats);

        Phase = BelotePhase.Finished;
        StopTurnTimer();
        ClearSubPanel();
        await _statsService.RecordDealAsync(Seats, ScoreResult);
        await RenderAllAsync();
        OnEnd();
    }

    /// <summary>
    /// Ends a deal nobody took: there is no result to show, so every panel and page is simply wiped.
    /// </summary>
    private void EndGame()
    {
        StopTurnTimer();
        Phase = BelotePhase.Finished;
        WipePublicPanel();
        ClearSubPanel();
        ClosePlayerPages();
        OnEnd();
    }

    #endregion

    #region Timeouts

    protected override async Task OnTurnTimeoutAsync()
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

    protected override IEnumerable<BelotePlayer> GetTurnWarningRecipients()
    {
        var player = Phase is BelotePhase.Bidding or BelotePhase.Playing ? CurrentPlayer : null;
        return player is null ? [] : [player];
    }

    #endregion

    protected override BeloteViewModel BuildModel(BelotePlayer viewer) => new()
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

    private static void SortHand(List<BeloteCard> hand, Suit? trump)
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

    private string GetSuitName(Suit suit) =>
        Context.GetString($"belote_suit_{suit.ToString().ToLowerInvariant()}");
}
