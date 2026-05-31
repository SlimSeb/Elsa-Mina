using ElsaMina.Commands.Economy;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.Poker;

public class PokerGame : Game, IPokerGame
{
    private static int _nextGameId;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IMoneyService _moneyService;

    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly PeriodicTimerRunner _turnTimer;
    private readonly List<PokerPlayer> _players = [];
    private readonly List<PokerCard> _community = [];
    private readonly List<PokerCard> _deck = [];
    private readonly List<PokerPot> _pots = [];
    private readonly HashSet<string> _initializedHandPanels = [];

    private int _dealerIndex;
    private int _currentTurnIndex = -1;
    private long _currentBet;
    private long _lastRaiseAmount;
    private bool _publicPanelInitialized;
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
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _moneyService = moneyService;
        GameId = Interlocked.Increment(ref _nextGameId);
        _turnTimer = new PeriodicTimerRunner(turnTimeout, OnTurnTimeoutAsync, runOnce: true);
    }

    public int GameId { get; }
    public override string Identifier => nameof(PokerGame);

    public IContext Context { get; set; }
    public long BuyIn { get; set; } = PokerConstants.DEFAULT_BUY_IN;

    public IReadOnlyList<PokerPlayer> Players => _players;
    public int PlayerCount => _players.Count;
    public PokerPhase Phase { get; private set; } = PokerPhase.Lobby;

    public PokerPlayer CurrentPlayer =>
        _currentTurnIndex >= 0 && _currentTurnIndex < _players.Count ? _players[_currentTurnIndex] : null;

    public IReadOnlyList<PokerCard> CommunityCards => _community;

    public long BigBlindAmount => PokerConstants.BigBlind(BuyIn);
    public long SmallBlindAmount => PokerConstants.SmallBlind(BuyIn);
    public long CurrentBet => _currentBet;
    public long LastRaiseAmount => _lastRaiseAmount;
    public long TotalPot => _players.Sum(player => player.Committed);

    public PokerPlayer Dealer => _players.Count > 0 ? _players[_dealerIndex] : null;
    public PokerPlayer SmallBlindPlayer => PositionPlayer(SmallBlindOffset());
    public PokerPlayer BigBlindPlayer => PositionPlayer(BigBlindOffset());

    public IReadOnlyList<PokerPot> Pots => _pots;
    public bool WentToShowdown { get; private set; }

    public long AmountToCall(PokerPlayer player) => Math.Max(0, _currentBet - player.RoundBet);

    public long MinimumRaiseTo() => _currentBet == 0 ? BigBlindAmount : _currentBet + _lastRaiseAmount;

    private string PublicPanelId => $"poker-{GameId}-{_publicPanelSegment}";
    private string HandPanelId(string userId) => $"poker-hand-{GameId}-{userId}-{_handPanelSegment}";

    #region Lobby

    public Task BeginJoinPhaseAsync() => RenderPublicAsync();

    public async Task<(bool Success, string MessageKey, object[] Args)> JoinAsync(IUser user)
    {
        await _actionLock.WaitAsync();
        try
        {
            if (Phase != PokerPhase.Lobby)
            {
                return (false, "poker_join_already_started", []);
            }

            if (_players.Count >= PokerConstants.MAX_PLAYERS)
            {
                return (false, "poker_join_full", []);
            }

            if (_players.Any(player => player.UserId == user.UserId))
            {
                return (false, "poker_join_already_joined", []);
            }

            var balance = await _moneyService.GetBalanceAsync(Context.RoomId, user.UserId);
            if (balance < BuyIn)
            {
                return (false, "poker_join_insufficient_funds", [BuyIn, balance]);
            }

            await _moneyService.AddAsync(Context.RoomId, user.UserId, -BuyIn);

            _players.Add(new PokerPlayer(user, BuyIn));
            await RenderPublicAsync();
            return (true, "poker_join_success", [user.Name, BuyIn]);
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
            if (Phase != PokerPhase.Lobby)
            {
                Context.ReplyLocalizedMessage("poker_start_already_started");
                return;
            }

            if (_players.All(player => player.UserId != user.UserId))
            {
                Context.ReplyLocalizedMessage("poker_start_not_a_player");
                return;
            }

            if (_players.Count < PokerConstants.MIN_PLAYERS)
            {
                Context.ReplyLocalizedMessage("poker_start_not_enough_players", PokerConstants.MIN_PLAYERS);
                return;
            }

            await DealAndStartHandAsync();
        }
        finally
        {
            _actionLock.Release();
        }
    }

    #endregion

    #region Dealing

    private async Task DealAndStartHandAsync()
    {
        OnStart();

        _randomService.ShuffleInPlace(_players);
        _dealerIndex = _randomService.NextInt(_players.Count);

        _deck.Clear();
        _deck.AddRange(PokerConstants.BuildDeck());
        _randomService.ShuffleInPlace(_deck);

        foreach (var player in _players)
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

        _currentBet = _players.Max(player => player.RoundBet);
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
        foreach (var other in _players.Where(other => other != player && other.CanAct))
        {
            other.HasActed = false;
        }

        await AdvanceAsync();
    }

    private bool IsActable(IUser user) =>
        Phase is PokerPhase.Preflop or PokerPhase.Flop or PokerPhase.Turn or PokerPhase.River
        && CurrentPlayer?.UserId == user.UserId;

    private void CommitChips(PokerPlayer player, long amount)
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
        foreach (var player in _players)
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
        _currentTurnIndex = FindNextActor(startExclusive);
    }

    private async Task AdvanceAsync()
    {
        if (_players.Count(player => !player.HasFolded) <= 1)
        {
            await ResolveHandAsync();
            return;
        }

        var next = FindNextActor(_currentTurnIndex);
        if (next >= 0)
        {
            _currentTurnIndex = next;
            await RenderAllAsync();
            RestartTurnTimer();
            return;
        }

        await ProceedToNextStreetAsync();
    }

    private async Task ProceedToNextStreetAsync()
    {
        // No more than one player can still act: deal the rest of the board, then showdown.
        if (_players.Count(player => player.CanAct) <= 1)
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
        for (var step = 1; step <= _players.Count; step++)
        {
            var index = (startExclusive + step) % _players.Count;
            var player = _players[index];
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

        var contenders = _players.Where(player => !player.HasFolded).ToList();
        WentToShowdown = contenders.Count >= 2;

        if (WentToShowdown)
        {
            foreach (var player in contenders)
            {
                player.Evaluation = PokerHandEvaluator.EvaluateBest([.. player.HoleCards, .. _community]);
            }
        }

        _pots.Clear();
        _pots.AddRange(PokerPotCalculator.BuildPots(_players));

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
        var eligible = _players
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
            .OrderBy(player => (_players.IndexOf(player) - _dealerIndex - 1 + _players.Count) % _players.Count)
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
        foreach (var player in _players)
        {
            var amount = payout(player);
            if (amount <= 0)
            {
                continue;
            }

            await _moneyService.AddAsync(Context.RoomId, player.UserId, amount);
        }
    }

    public async Task CancelAsync()
    {
        await _actionLock.WaitAsync();
        try
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
            foreach (var player in _players)
            {
                Context.SendPrivateUpdatableHtml(player.UserId, Context.RoomId, HandPanelId(player.UserId),
                    string.Empty, true);
            }

            OnEnd();
        }
        finally
        {
            _actionLock.Release();
        }
    }

    #endregion

    #region Timeout & helpers

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
            var player = CurrentPlayer;
            if (player is null || Phase is not (PokerPhase.Preflop or PokerPhase.Flop or PokerPhase.Turn
                    or PokerPhase.River))
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
        finally
        {
            _actionLock.Release();
        }
    }

    private void RestartTurnTimer()
    {
        if (Phase is PokerPhase.Preflop or PokerPhase.Flop or PokerPhase.Turn or PokerPhase.River)
        {
            _turnTimer.Restart();
        }
    }

    private void StopTurnTimer() => _turnTimer.Stop();

    private int SmallBlindOffset() => _players.Count == 2 ? 0 : 1;
    private int BigBlindOffset() => _players.Count == 2 ? 1 : 2;

    private int PositionIndex(int offset) => (_dealerIndex + offset) % _players.Count;
    private PokerPlayer PositionPlayer(int offset) => _players.Count > 0 ? _players[PositionIndex(offset)] : null;

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
            PokerPhase.Lobby => "Games/Poker/PokerLobby",
            PokerPhase.Finished => "Games/Poker/PokerResult",
            _ => "Games/Poker/PokerTable"
        };

        var html = await _templatesManager.GetTemplateAsync(templateKey, BuildModel(null));
        Context.SendUpdatableHtml(PublicPanelId, html.RemoveNewlines(), _publicPanelInitialized);
        _publicPanelInitialized = true;
    }

    private async Task RenderHandsAsync()
    {
        foreach (var player in _players)
        {
            var html = await _templatesManager.GetTemplateAsync("Games/Poker/PokerHand", BuildModel(player));
            var alreadyInitialized = _initializedHandPanels.Contains(player.UserId);
            Context.SendPrivateUpdatableHtml(player.UserId, Context.RoomId, HandPanelId(player.UserId),
                html.RemoveNewlines(), alreadyInitialized);
            _initializedHandPanels.Add(player.UserId);
        }
    }

    private void RepostPanels()
    {
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
    }

    private PokerViewModel BuildModel(PokerPlayer viewer) => new()
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
