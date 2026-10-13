using ElsaMina.Commands.Games.Blackjack;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Blackjack;

public class BlackjackGameTest
{
    private IRandomService _randomService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private IUser _owner;
    private BlackjackGame _game;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _configuration.Name.Returns("Bot");
        _configuration.Trigger.Returns("-");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult("<div>\nboard\n</div>"));

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");
        _owner = Substitute.For<IUser>();
        _owner.UserId.Returns("player");

        _game = new BlackjackGame(_randomService, _templatesManager, _configuration)
        {
            Context = _context,
            Owner = _owner
        };
    }

    [TearDown]
    public async Task TearDown()
    {
        // Stops the timeout timer of games left in progress
        await _game.CancelAsync();
    }

    // Arranges the deck so the cards are drawn in the given order: player, dealer, player, dealer, then hits
    private void StackDeck(params int[] ranksInDrawOrder)
    {
        _randomService.When(service => service.ShuffleInPlace(Arg.Any<IList<BlackjackCard>>())).Do(callInfo =>
        {
            var deck = callInfo.Arg<IList<BlackjackCard>>();
            var drawnCards = new List<BlackjackCard>();
            foreach (var rank in ranksInDrawOrder)
            {
                var card = deck.First(deckCard => deckCard.Rank == rank);
                deck.Remove(card);
                drawnCards.Add(card);
            }

            // Cards are drawn from the end of the deck
            for (var i = drawnCards.Count - 1; i >= 0; i--)
            {
                deck.Add(drawnCards[i]);
            }
        });
    }

    private static IUser CreateUser(string userId)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(userId);
        return user;
    }

    [Test]
    public void Test_Identifier_ShouldReturnBlackjackGame()
    {
        Assert.That(_game.Identifier, Is.EqualTo("BlackjackGame"));
    }

    [Test]
    public async Task Test_DisplayAnnounce_ShouldSendAnnounceAsUpdatableHtml()
    {
        // Act
        await _game.DisplayAnnounce();

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Games/Blackjack/BlackjackAnnounce",
            Arg.Is<BlackjackViewModel>(model => model.BotName == "Bot" && model.Trigger == "-"
                                                && model.RoomId == "arcade"));
        _context.Received(1).SendUpdatableHtml(Arg.Is<string>(id => id.StartsWith("bj-arcade-")),
            "<div>board</div>", false);
    }

    [Test]
    public async Task Test_StartGame_ShouldDealTwoCardsEach_AndWaitForPlayer()
    {
        // Arrange
        StackDeck(10, 9, 7, 8);

        // Act
        await _game.StartGame();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.State, Is.EqualTo(BlackjackGameState.PlayerTurn));
            Assert.That(_game.PlayerHand.Value, Is.EqualTo(17));
            Assert.That(_game.DealerHand.Value, Is.EqualTo(17));
            Assert.That(_game.IsStarted, Is.True);
            Assert.That(_game.IsOver, Is.False);
        }
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), "<div>board</div>", false);
    }

    [Test]
    public async Task Test_StartGame_ShouldUpdateAnnounce_WhenAnnounceWasDisplayed()
    {
        // Arrange
        StackDeck(10, 9, 7, 8);
        await _game.DisplayAnnounce();

        // Act
        await _game.StartGame();

        // Assert
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), true);
    }

    [Test]
    public async Task Test_StartGame_ShouldMakePlayerWin_WhenPlayerHasBlackjack()
    {
        // Arrange
        StackDeck(1, 10, 13, 9);
        var ended = false;
        _game.GameEnded += () => ended = true;

        // Act
        await _game.StartGame();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.State, Is.EqualTo(BlackjackGameState.PlayerWon));
            Assert.That(_game.IsOver, Is.True);
            Assert.That(ended, Is.True);
        }
    }

    [Test]
    public async Task Test_StartGame_ShouldTie_WhenBothHaveBlackjack()
    {
        // Arrange
        StackDeck(1, 1, 12, 11);

        // Act
        await _game.StartGame();

        // Assert
        Assert.That(_game.State, Is.EqualTo(BlackjackGameState.Tie));
    }

    [Test]
    public async Task Test_StartGame_ShouldSendPrivateHtml_WhenInPrivateMode()
    {
        // Arrange
        StackDeck(10, 9, 7, 8);
        _game.IsPrivateMode = true;
        _game.TargetRoomId = "casino";
        _game.TargetUserId = "player";

        // Act
        await _game.StartGame();

        // Assert
        _context.Received(1).SendPrivateUpdatableHtml("player", "casino",
            Arg.Is<string>(id => id.StartsWith("bj-casino-")), "<div>board</div>", false);
        _context.DidNotReceive().SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_Hit_ShouldAddCardAndKeepPlaying_WhenPlayerIsUnder21()
    {
        // Arrange
        StackDeck(2, 9, 3, 8, 4);
        await _game.StartGame();

        // Act
        await _game.Hit(_owner);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.PlayerHand.Cards, Has.Count.EqualTo(3));
            Assert.That(_game.PlayerHand.Value, Is.EqualTo(9));
            Assert.That(_game.State, Is.EqualTo(BlackjackGameState.PlayerTurn));
        }
    }

    [Test]
    public async Task Test_Hit_ShouldEndGame_WhenPlayerBusts()
    {
        // Arrange
        StackDeck(10, 9, 6, 8, 13);
        await _game.StartGame();

        // Act
        await _game.Hit(_owner);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.State, Is.EqualTo(BlackjackGameState.PlayerBust));
            Assert.That(_game.IsEnded, Is.True);
        }
    }

    [Test]
    public async Task Test_Hit_ShouldDoNothing_WhenUserIsNotOwner()
    {
        // Arrange
        StackDeck(10, 9, 6, 8, 13);
        await _game.StartGame();

        // Act
        await _game.Hit(CreateUser("someoneelse"));

        // Assert
        Assert.That(_game.PlayerHand.Cards, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Test_Hit_ShouldDoNothing_WhenGameIsNotStarted()
    {
        // Act
        await _game.Hit(_owner);

        // Assert
        Assert.That(_game.PlayerHand, Is.Null);
    }

    [Test]
    public async Task Test_Stand_ShouldMakeDealerDrawUntilSeventeen_AndPlayerWin_WhenDealerBusts()
    {
        // Arrange
        StackDeck(10, 10, 8, 2, 4, 13);
        await _game.StartGame();

        // Act
        await _game.Stand(_owner);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.DealerHand.Cards, Has.Count.EqualTo(4));
            Assert.That(_game.DealerHand.IsBust, Is.True);
            Assert.That(_game.State, Is.EqualTo(BlackjackGameState.PlayerWon));
            Assert.That(_game.IsEnded, Is.True);
        }
    }

    [Test]
    public async Task Test_Stand_ShouldMakePlayerWin_WhenPlayerHasHigherValue()
    {
        // Arrange
        StackDeck(10, 10, 9, 7);
        await _game.StartGame();

        // Act
        await _game.Stand(_owner);

        // Assert
        Assert.That(_game.State, Is.EqualTo(BlackjackGameState.PlayerWon));
    }

    [Test]
    public async Task Test_Stand_ShouldMakeDealerWin_WhenDealerHasHigherValue()
    {
        // Arrange
        StackDeck(10, 10, 7, 9);
        await _game.StartGame();

        // Act
        await _game.Stand(_owner);

        // Assert
        Assert.That(_game.State, Is.EqualTo(BlackjackGameState.DealerWon));
    }

    [Test]
    public async Task Test_Stand_ShouldTie_WhenValuesAreEqual()
    {
        // Arrange
        StackDeck(10, 10, 8, 8);
        await _game.StartGame();

        // Act
        await _game.Stand(_owner);

        // Assert
        Assert.That(_game.State, Is.EqualTo(BlackjackGameState.Tie));
    }

    [Test]
    public async Task Test_Stand_ShouldDoNothing_WhenUserIsNotOwner()
    {
        // Arrange
        StackDeck(10, 10, 8, 8);
        await _game.StartGame();

        // Act
        await _game.Stand(CreateUser("someoneelse"));

        // Assert
        Assert.That(_game.State, Is.EqualTo(BlackjackGameState.PlayerTurn));
    }

    [Test]
    public async Task Test_CancelAsync_ShouldEndGameAndRedrawBoard()
    {
        // Arrange
        StackDeck(10, 10, 8, 8);
        await _game.StartGame();

        // Act
        await _game.CancelAsync();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsEnded, Is.True);
            Assert.That(_game.IsOver, Is.True);
        }
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), true);
    }
}
