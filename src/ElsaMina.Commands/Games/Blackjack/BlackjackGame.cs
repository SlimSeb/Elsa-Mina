using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Blackjack;

public class BlackjackGame
{
    private static int _nextGameId;
    private readonly List<BlackjackCard> _deck;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly int _gameId;

    public BlackjackHand PlayerHand { get; } = new();
    public BlackjackHand DealerHand { get; } = new();
    public BlackjackGameState State { get; private set; } = BlackjackGameState.PlayerTurn;
    public IContext Context { get; set; }
    public IUser Player { get; set; }

    public BlackjackGame(IRandomService randomService, ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _templatesManager = templatesManager;
        _configuration = configuration;
        _deck = CreateShuffledDeck(randomService);
        _gameId = _nextGameId++;

        PlayerHand.Add(DrawCard());
        DealerHand.Add(DrawCard());
        PlayerHand.Add(DrawCard());
        DealerHand.Add(DrawCard());

        if (PlayerHand.IsBlackjack && DealerHand.IsBlackjack)
        {
            State = BlackjackGameState.Tie;
        }
        else if (PlayerHand.IsBlackjack)
        {
            State = BlackjackGameState.PlayerWon;
        }
    }

    public async Task Hit()
    {
        if (State != BlackjackGameState.PlayerTurn)
        {
            return;
        }

        PlayerHand.Add(DrawCard());
        if (PlayerHand.IsBust)
        {
            State = BlackjackGameState.PlayerBust;
        }

        await DisplayTableAsync();
    }

    public async Task Stand()
    {
        if (State != BlackjackGameState.PlayerTurn)
        {
            return;
        }

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

        await DisplayTableAsync();
    }

    public bool IsOver => State != BlackjackGameState.PlayerTurn;

    public async Task DisplayTableAsync()
    {
        var template = await _templatesManager.GetTemplateAsync("Games/Blackjack/BlackjackTable",
            new BlackjackViewModel
            {
                Culture = Context.Culture,
                Game = this,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = Context.RoomId
            });

        var htmlId = $"bj-{Context.RoomId}-{Player.UserId}-{_gameId}";
        Context.SendPrivateUpdatableHtml(Player.UserId, Context.RoomId, htmlId, template.RemoveNewlines(), true);
    }

    private BlackjackCard DrawCard()
    {
        var card = _deck[^1];
        _deck.RemoveAt(_deck.Count - 1);
        return card;
    }

    private static List<BlackjackCard> CreateShuffledDeck(IRandomService randomService)
    {
        var deck = new List<BlackjackCard>(52);
        foreach (var suit in BlackjackConstants.SUITS)
        {
            for (var rank = 1; rank <= 13; rank++)
            {
                deck.Add(new BlackjackCard(rank, suit));
            }
        }
        randomService.ShuffleInPlace(deck);
        return deck;
    }
}
