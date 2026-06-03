using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Blackjack;

public class BlackjackGame : Game, IBlackjackGame
{
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(20);

    private static int NextGameId { get; set; } = 1;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    private readonly int _gameId;
    private List<BlackjackCard> _deck;
    private PeriodicTimerRunner _timeoutTimer;
    private bool _htmlInitialized;

    public BlackjackGame(IRandomService randomService, ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(BlackjackGame);

    public BlackjackHand PlayerHand { get; private set; }
    public BlackjackHand DealerHand { get; private set; }
    public BlackjackGameState State { get; private set; }
    public bool IsPrivateMode { get; set; }
    public string TargetRoomId { get; set; }
    public string TargetUserId { get; set; }
    public IContext Context { get; set; }
    public IUser Owner { get; set; }

    public bool IsOver => IsEnded || State != BlackjackGameState.PlayerTurn;

    private string EffectiveRoomId => IsPrivateMode ? TargetRoomId : Context.RoomId;
    private string GameIdentifier => $"bj-{EffectiveRoomId}-{_gameId}";

    public async Task DisplayAnnounce()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/Blackjack/BlackjackAnnounce",
            new BlackjackViewModel
            {
                Culture = Context.Culture,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = EffectiveRoomId
            });

        Context.SendUpdatableHtml(GameIdentifier, template.RemoveNewlines(), false);
        _htmlInitialized = true;
    }

    public async Task StartGame()
    {
        PlayerHand = new BlackjackHand();
        DealerHand = new BlackjackHand();
        _deck = CreateShuffledDeck();

        PlayerHand.Add(DrawCard());
        DealerHand.Add(DrawCard());
        PlayerHand.Add(DrawCard());
        DealerHand.Add(DrawCard());

        OnStart();

        if (PlayerHand.IsBlackjack && DealerHand.IsBlackjack)
        {
            State = BlackjackGameState.Tie;
        }
        else if (PlayerHand.IsBlackjack)
        {
            State = BlackjackGameState.PlayerWon;
        }
        else
        {
            State = BlackjackGameState.PlayerTurn;
        }

        await DisplayBoard();

        if (State != BlackjackGameState.PlayerTurn)
        {
            OnEnd();
        }
        else
        {
            StartTimeoutTimer();
        }
    }

    public async Task Hit(IUser user)
    {
        if (!IsStarted || State != BlackjackGameState.PlayerTurn || user.UserId != Owner.UserId)
        {
            return;
        }

        PlayerHand.Add(DrawCard());

        if (PlayerHand.IsBust)
        {
            State = BlackjackGameState.PlayerBust;
        }

        await DisplayBoard();

        if (State != BlackjackGameState.PlayerTurn)
        {
            CancelTimeoutTimer();
            OnEnd();
        }
        else
        {
            RestartTimeoutTimer();
        }
    }

    public async Task Stand(IUser user)
    {
        if (!IsStarted || State != BlackjackGameState.PlayerTurn || user.UserId != Owner.UserId)
        {
            return;
        }

        CancelTimeoutTimer();

        while (DealerHand.Value < BlackjackConstants.DEALER_STAND_THRESHOLD)
        {
            DealerHand.Add(DrawCard());
        }

        if (DealerHand.IsBust || PlayerHand.Value > DealerHand.Value)
        {
            State = BlackjackGameState.PlayerWon;
        }
        else if (DealerHand.Value > PlayerHand.Value)
        {
            State = BlackjackGameState.DealerWon;
        }
        else
        {
            State = BlackjackGameState.Tie;
        }

        await DisplayBoard();
        OnEnd();
    }

    public async Task CancelAsync()
    {
        CancelTimeoutTimer();
        OnEnd();
        await DisplayBoard();
    }

    private void StartTimeoutTimer()
    {
        _timeoutTimer = new PeriodicTimerRunner(TIMEOUT, HandleTimeoutAsync, runOnce: true);
        _timeoutTimer.Start();
    }

    private void RestartTimeoutTimer()
    {
        _timeoutTimer?.Restart();
    }

    private void CancelTimeoutTimer()
    {
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
    }

    private async Task HandleTimeoutAsync()
    {
        if (!IsStarted || State != BlackjackGameState.PlayerTurn)
        {
            return;
        }
        await CancelAsync();
    }

    private async Task DisplayBoard()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/Blackjack/BlackjackTable",
            new BlackjackViewModel
            {
                Culture = Context.Culture,
                Game = this,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = EffectiveRoomId
            });

        var isChanging = _htmlInitialized;
        _htmlInitialized = true;

        if (IsPrivateMode)
        {
            Context.SendPrivateUpdatableHtml(TargetUserId, TargetRoomId, GameIdentifier,
                template.RemoveNewlines(), isChanging);
        }
        else
        {
            Context.SendUpdatableHtml(GameIdentifier, template.RemoveNewlines(), isChanging);
        }
    }

    private List<BlackjackCard> CreateShuffledDeck()
    {
        var deck = new List<BlackjackCard>(52);
        foreach (var suit in BlackjackConstants.SUITS)
        {
            for (var rank = 1; rank <= 13; rank++)
            {
                deck.Add(new BlackjackCard(rank, suit));
            }
        }
        _randomService.ShuffleInPlace(deck);
        return deck;
    }

    private BlackjackCard DrawCard()
    {
        var card = _deck[^1];
        _deck.RemoveAt(_deck.Count - 1);
        return card;
    }
}
