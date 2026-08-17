using ElsaMina.Commands.Economy;
using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.Poker;

public class PokerGame : SeatedCardGame<PokerPlayer>, IPokerGame
{
    private readonly IRandomService _randomService;
    private readonly IConfiguration _configuration;
    private readonly IMoneyService _moneyService;

    private readonly List<PokerCard> _community = [];
    private readonly List<PokerCard> _deck = [];
    private readonly List<PokerPot> _pots = [];
    private readonly HashSet<string> _initializedHandPanels = [];

    private int _dealerIndex;
    private long _currentBet;
    private long _lastRaiseAmount;
    private int _publicPanelSegment;
    private int _handPanelSegment;

    [UsedImplicitly]
    public PokerGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        IMoneyService moneyService)
        : this(randomService, templatesManager, configuration, moneyService, PokerConstants.TURN_TIMEOUT)
    {
    }

    public PokerGame(IRandomService randomService, ITemplatesManager templatesManager, IConfiguration configuration,
        IMoneyService moneyService, TimeSpan turnTimeout)
        // Poker gives no advance warning before a turn runs out: it simply checks or folds for you.
        : base(templatesManager, turnTimeout, turnWarningRemaining: null)
    {
        _randomService = randomService;
        _configuration = configuration;
        _moneyService = moneyService;
        CurrentTurnIndex = -1;
    }

    public override string Identifier => nameof(PokerGame);

    public long BuyIn { get; set; } = PokerConstants.DEFAULT_BUY_IN;

    // When bucks are disabled in the room, poker runs as a "for fun" mode:
    // the buy-in only seeds each player's chip stack and no real bucks are moved.
    public bool IsForFun { get; set; }

    public PokerPhase Phase { get; private set; } = PokerPhase.Lobby;

    public override bool IsInLobby => Phase == PokerPhase.Lobby;

    protected override string ResourcePrefix => "poker";
    protected override string TemplateFolder => "Poker";
    protected override int MinPlayers => PokerConstants.MIN_PLAYERS;
    protected override int MaxPlayers => PokerConstants.MAX_PLAYERS;
    protected override bool IsFinished => Phase == PokerPhase.Finished;

    protected override bool IsAcceptingActions =>
        Phase is PokerPhase.Preflop or PokerPhase.Flop or PokerPhase.Turn or PokerPhase.River;

    protected override PokerPlayer CreatePlayer(IUser user) => new(user, BuyIn);

    public PokerPlayer CurrentPlayer => CurrentSeat;

    public IReadOnlyList<PokerCard> CommunityCards => _community;

    public long BigBlindAmount => PokerConstants.BigBlind(BuyIn);
    public long SmallBlindAmount => PokerConstants.SmallBlind(BuyIn);
    public long CurrentBet => _currentBet;
    public long LastRaiseAmount => _lastRaiseAmount;
    public long TotalPot => Seats.Sum(player => player.Committed);

    public PokerPlayer Dealer => Seats.Count > 0 ? Seats[_dealerIndex] : null;
    public PokerPlayer SmallBlindPlayer => PositionPlayer(SmallBlindOffset());
    public PokerPlayer BigBlindPlayer => PositionPlayer(BigBlindOffset());

    public IReadOnlyList<PokerPot> Pots => _pots;
    public bool WentToShowdown { get; private set; }

    public long AmountToCall(PokerPlayer player) => Math.Max(0, _currentBet - player.RoundBet);

    public long MinimumRaiseTo() => _currentBet == 0 ? BigBlindAmount : _currentBet + _lastRaiseAmount;

    // Every re-post moves the panels to a new id, so the wiped ones stay wiped in the scrollback.
    protected override string PublicPanelId => $"poker-{GameId}-{_publicPanelSegment}";
    private string HandPanelId(string userId) => $"poker-hand-{GameId}-{userId}-{_handPanelSegment}";

    #region Lobby

    /// <summary>
    /// Takes the buy-in out of the joining player's bucks, unless the table runs for fun.
    /// </summary>
    protected override async Task<(bool Success, string MessageKey, object[] Args)?> OnJoiningAsync(IUser user)
    {
        if (IsForFun)
        {
            return null;
        }

        var balance = await _moneyService.GetBalanceAsync(Context.RoomId, user.UserId);
        if (balance < BuyIn)
        {
            return (false, "poker_join_insufficient_funds", [BuyIn, balance]);
        }

        await _moneyService.AddAsync(Context.RoomId, user.UserId, -BuyIn);
        return null;
    }

    protected override object[] JoinSuccessArguments(IUser user) => [user.Name, BuyIn];

    #endregion

    #region Dealing

    protected override async Task StartDealAsync()
    {
        OnStart();

        _randomService.ShuffleInPlace(Seats);
        _dealerIndex = _randomService.NextInt(Seats.Count);

        _deck.Clear();
        _deck.AddRange(PokerConstants.BuildDeck());
        _randomService.ShuffleInPlace(_deck);

        foreach (var player in Seats)
        {
            player.HoleCards.Add(DrawCard());
            player.HoleCards.Add(DrawCard());
        }

        Phase = PokerPhase.Preflop;
        PostBlinds();
        StartBettingRound(preflop: true);

        await RenderAllAsync();
        RestartTurnTimer();
    }

    private void PostBlinds()
    {
        var smallBlind = SmallBlindPlayer;
        var bigBlind = BigBlindPlayer;

        CommitChips(smallBlind, Math.Min(SmallBlindAmount, smallBlind.Stack));
        CommitChips(bigBlind, Math.Min(BigBlindAmount, bigBlind.Stack));

        _currentBet = Seats.Max(player => player.RoundBet);
        _lastRaiseAmount = BigBlindAmount;
    }

    private PokerCard DrawCard()
    {
        var card = _deck[^1];
        _deck.RemoveAt(_deck.Count - 1);
        return card;
    }

    #endregion

    #region Actions

    public Task FoldAsync(IUser user) => RunActionAsync(() => FoldCoreAsync(user));
    public Task CheckAsync(IUser user) => RunActionAsync(() => CheckCoreAsync(user));
    public Task CallAsync(IUser user) => RunActionAsync(() => CallCoreAsync(user));
    public Task RaiseAsync(IUser user, long amountTo) => RunActionAsync(() => RaiseCoreAsync(user, amountTo));

    private async Task FoldCoreAsync(IUser user)
    {
        if (!IsActable(user))
        {
            return;
        }

        var player = CurrentPlayer;
        player.HasFolded = true;
        player.HasActed = true;
        await AdvanceAsync();
    }

    private async Task CheckCoreAsync(IUser user)
    {
        if (!IsActable(user))
        {
            return;
        }

        var player = CurrentPlayer;
        if (AmountToCall(player) > 0)
        {
            Context.ReplyLocalizedMessage("poker_cannot_check", player.Name, AmountToCall(player));
            return;
        }

        player.HasActed = true;
        await AdvanceAsync();
    }

    private async Task CallCoreAsync(IUser user)
    {
        if (!IsActable(user))
        {
            return;
        }

        var player = CurrentPlayer;
        var toCall = Math.Min(AmountToCall(player), player.Stack);
        CommitChips(player, toCall);
        player.HasActed = true;
        await AdvanceAsync();
    }

    private async Task RaiseCoreAsync(IUser user, long amountTo)
    {
        if (!IsActable(user))
        {
            return;
        }

        var player = CurrentPlayer;
        var maxTo = player.RoundBet + player.Stack;

        if (amountTo <= _currentBet)
        {
            Context.ReplyLocalizedMessage("poker_raise_too_low", player.Name, MinimumRaiseTo());
            return;
        }

        var isAllIn = amountTo >= maxTo;
        if (isAllIn)
        {
            amountTo = maxTo;
        }
        else if (amountTo < MinimumRaiseTo())
        {
            Context.ReplyLocalizedMessage("poker_raise_too_low", player.Name, MinimumRaiseTo());
            return;
        }

        var raiseIncrement = amountTo - _currentBet;
        CommitChips(player, amountTo - player.RoundBet);

        _lastRaiseAmount = Math.Max(_lastRaiseAmount, raiseIncrement);
        _currentBet = amountTo;
        player.HasActed = true;

        // A raise reopens the action: everyone still able to act must respond to it.
        foreach (var other in Seats.Where(other => other != player && other.CanAct))
        {
            other.HasActed = false;
        }

        await AdvanceAsync();
    }

    private bool IsActable(IUser user) => IsAcceptingActions && CurrentPlayer?.UserId == user.UserId;

    private static void CommitChips(PokerPlayer player, long amount)
    {
        var actual = Math.Min(amount, player.Stack);
        player.Stack -= actual;
        player.Committed += actual;
        player.RoundBet += actual;
    }

    #endregion

    #region Round progression

    private void StartBettingRound(bool preflop)
    {
        foreach (var player in Seats)
        {
            player.RoundBet = preflop ? player.RoundBet : 0;
            player.HasActed = false;
        }

        if (!preflop)
        {
            _currentBet = 0;
            _lastRaiseAmount = BigBlindAmount;
        }

        // Preflop the first actor sits left of the big blind; afterwards, left of the dealer.
        var startExclusive = preflop ? PositionIndex(BigBlindOffset()) : _dealerIndex;
        CurrentTurnIndex = FindNextActor(startExclusive);
    }

    private async Task AdvanceAsync()
    {
        if (Seats.Count(player => !player.HasFolded) <= 1)
        {
            await ResolveHandAsync();
            return;
        }

        var next = FindNextActor(CurrentTurnIndex);
        if (next >= 0)
        {
            CurrentTurnIndex = next;
            await RenderAllAsync();
            RestartTurnTimer();
            return;
        }

        await ProceedToNextStreetAsync();
    }

    private async Task ProceedToNextStreetAsync()
    {
        // No more than one player can still act: deal the rest of the board, then showdown.
        if (Seats.Count(player => player.CanAct) <= 1)
        {
            DealRemainingBoard();
            await ResolveHandAsync();
            return;
        }

        switch (Phase)
        {
            case PokerPhase.Preflop:
                Phase = PokerPhase.Flop;
                DealCommunity(3);
                break;
            case PokerPhase.Flop:
                Phase = PokerPhase.Turn;
                DealCommunity(1);
                break;
            case PokerPhase.Turn:
                Phase = PokerPhase.River;
                DealCommunity(1);
                break;
            case PokerPhase.River:
                await ResolveHandAsync();
                return;
        }

        StartBettingRound(preflop: false);
        RepostPanels();
        await RenderAllAsync();
        RestartTurnTimer();
    }

    private void DealCommunity(int count)
    {
        for (var card = 0; card < count; card++)
        {
            _community.Add(DrawCard());
        }
    }

    private void DealRemainingBoard()
    {
        if (_community.Count == 0)
        {
            DealCommunity(3);
        }

        while (_community.Count < 5)
        {
            DealCommunity(1);
        }

        Phase = PokerPhase.River;
    }

    private int FindNextActor(int startExclusive)
    {
        for (var step = 1; step <= Seats.Count; step++)
        {
            var index = (startExclusive + step) % Seats.Count;
            var player = Seats[index];
            if (player.CanAct && (!player.HasActed || player.RoundBet < _currentBet))
            {
                return index;
            }
        }

        return -1;
    }

    #endregion

    #region Showdown & settlement

    private async Task ResolveHandAsync()
    {
        StopTurnTimer();
        Phase = PokerPhase.Showdown;

        var contenders = Seats.Where(player => !player.HasFolded).ToList();
        WentToShowdown = contenders.Count >= 2;

        if (WentToShowdown)
        {
            foreach (var player in contenders)
            {
                player.Evaluation = PokerHandEvaluator.EvaluateBest([.. player.HoleCards, .. _community]);
            }
        }

        _pots.Clear();
        _pots.AddRange(PokerPotCalculator.BuildPots(Seats));

        foreach (var pot in _pots)
        {
            AwardPot(pot);
        }

        await SettleAsync(player => player.Stack);

        Phase = PokerPhase.Finished;
        RepostPanels();
        await RenderPublicAsync();
        OnEnd();
    }

    private void AwardPot(PokerPot pot)
    {
        var eligible = Seats
            .Where(player => !player.HasFolded && pot.EligiblePlayerIds.Contains(player.UserId))
            .ToList();

        if (eligible.Count == 0)
        {
            return;
        }

        List<PokerPlayer> winners;
        if (WentToShowdown)
        {
            var best = eligible.Max(player => player.Evaluation);
            winners = eligible.Where(player => player.Evaluation.CompareTo(best) == 0).ToList();
        }
        else
        {
            winners = eligible;
        }

        // Odd chips go to the winners closest to the left of the dealer.
        winners = winners
            .OrderBy(player => (SeatIndexOf(player) - _dealerIndex - 1 + Seats.Count) % Seats.Count)
            .ToList();

        var share = pot.Amount / winners.Count;
        var remainder = pot.Amount % winners.Count;

        for (var i = 0; i < winners.Count; i++)
        {
            var awarded = share + (i < remainder ? 1 : 0);
            winners[i].Stack += awarded;
            winners[i].Winnings += awarded;
        }
    }

    private async Task SettleAsync(Func<PokerPlayer, long> payout)
    {
        if (IsForFun)
        {
            return;
        }

        foreach (var player in Seats)
        {
            var amount = payout(player);
            if (amount <= 0)
            {
                continue;
            }

            await _moneyService.AddAsync(Context.RoomId, player.UserId, amount);
        }
    }

    public override Task CancelAsync() => RunActionAsync(async () =>
    {
        if (Phase == PokerPhase.Finished)
        {
            return;
        }

        StopTurnTimer();

        // Refund every player what they still own: their stack plus whatever they put in the pot.
        await SettleAsync(player => player.Stack + player.Committed);

        Phase = PokerPhase.Finished;
        Context.SendUpdatableHtml(PublicPanelId, string.Empty, true);
        foreach (var player in Seats)
        {
            Context.SendPrivateUpdatableHtml(player.UserId, Context.RoomId, HandPanelId(player.UserId),
                string.Empty, true);
        }

        OnEnd();
    });

    #endregion

    #region Timeouts & positions

    protected override async Task OnTurnTimeoutAsync()
    {
        var player = CurrentPlayer;
        if (player is null || !IsAcceptingActions)
        {
            return;
        }

        if (AmountToCall(player) == 0)
        {
            await CheckCoreAsync(player.User);
        }
        else
        {
            await FoldCoreAsync(player.User);
        }
    }

    private int SmallBlindOffset() => Seats.Count == 2 ? 0 : 1;
    private int BigBlindOffset() => Seats.Count == 2 ? 1 : 2;

    private int PositionIndex(int offset) => (_dealerIndex + offset) % Seats.Count;
    private PokerPlayer PositionPlayer(int offset) => Seats.Count > 0 ? Seats[PositionIndex(offset)] : null;

    #endregion

    #region Rendering

    private async Task RenderAllAsync()
    {
        await RenderPublicAsync();
        await RenderHandsAsync();
    }

    /// <summary>
    /// Pushes each player their hole cards and action buttons as a private chat panel. Poker uses
    /// these rather than HTML pages so the buttons sit right next to the table in the room.
    /// </summary>
    private async Task RenderHandsAsync()
    {
        foreach (var player in Seats)
        {
            var html = await TemplatesManager.GetTemplateAsync(TemplateKey("Hand"), BuildModel(player));
            var alreadyInitialized = _initializedHandPanels.Contains(player.UserId);
            Context.SendPrivateUpdatableHtml(player.UserId, Context.RoomId, HandPanelId(player.UserId),
                html.RemoveNewlines(), alreadyInitialized);
            _initializedHandPanels.Add(player.UserId);
        }
    }

    /// <summary>
    /// Wipes every panel and moves on to the next segment, so the new street is posted at the bottom
    /// of the chat instead of updating panels stuck high up in the scrollback.
    /// </summary>
    private void RepostPanels()
    {
        WipePublicPanel();
        _publicPanelSegment++;

        foreach (var player in Seats)
        {
            Context.SendPrivateUpdatableHtml(player.UserId, Context.RoomId, HandPanelId(player.UserId),
                string.Empty, true);
        }

        _handPanelSegment++;
        _initializedHandPanels.Clear();
    }

    protected override PokerViewModel BuildModel(PokerPlayer viewer) => new()
    {
        Culture = Context.Culture,
        BotName = _configuration.Name,
        Trigger = _configuration.Trigger,
        RoomId = Context.RoomId,
        Game = this,
        Viewer = viewer
    };

    #endregion
}
