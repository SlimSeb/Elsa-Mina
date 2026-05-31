using System.Globalization;
using ElsaMina.Commands.Economy;
using ElsaMina.Commands.Games.Poker;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Poker;

[TestFixture]
public class PokerGameTest
{
    private const string ROOM_ID = "testroom";

    private FakeMoneyService _moneyService;
    private IRandomService _randomService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private PokerGame _game;

    [SetUp]
    public void SetUp()
    {
        _moneyService = new FakeMoneyService();
        _randomService = Substitute.For<IRandomService>(); // ShuffleInPlace no-op, NextInt -> 0 -> deterministic
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");
        _context.RoomId.Returns(ROOM_ID);
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        // A long timeout so the turn timer never fires during the test.
        _game = new PokerGame(_randomService, _templatesManager, _configuration, _moneyService, TimeSpan.FromHours(1))
        {
            Context = _context,
            BuyIn = 100
        };
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    private static IUser User(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return user;
    }

    private async Task PlayCheckOrCallUntilFinishedAsync()
    {
        var guard = 0;
        while (_game.Phase != PokerPhase.Finished && guard++ < 50)
        {
            var player = _game.CurrentPlayer;
            if (player is null)
            {
                break;
            }

            if (_game.AmountToCall(player) == 0)
            {
                await _game.CheckAsync(player.User);
            }
            else
            {
                await _game.CallAsync(player.User);
            }
        }
    }

    private async Task JoinBothAsync()
    {
        _moneyService.Seed(ROOM_ID, "player1", 100);
        _moneyService.Seed(ROOM_ID, "player2", 100);
        var users = new[] { User("player1"), User("player2") };
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
    }

    [Test]
    public async Task Test_Join_ShouldDeductBuyIn_AndSeatPlayer()
    {
        _moneyService.Seed(ROOM_ID, "player1", 250);

        var (success, _, _) = await _game.JoinAsync(User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.PlayerCount, Is.EqualTo(1));
            Assert.That(_moneyService.GetBalance("player1"), Is.EqualTo(150));
            Assert.That(_game.Players[0].Stack, Is.EqualTo(100));
        }
    }

    [Test]
    public async Task Test_Join_ShouldUseDefaultBalance_WhenPlayerHasNoData()
    {
        // No seed: the player should start at the default balance of 100 and be able to buy in.
        var (success, _, _) = await _game.JoinAsync(User("newbie"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_moneyService.GetBalance("newbie"), Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Test_Join_ShouldFail_WhenBalanceIsInsufficient()
    {
        _moneyService.Seed(ROOM_ID, "player1", 50);

        var (success, messageKey, _) = await _game.JoinAsync(User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("poker_join_insufficient_funds"));
            Assert.That(_game.PlayerCount, Is.EqualTo(0));
            Assert.That(_moneyService.GetBalance("player1"), Is.EqualTo(50));
        }
    }

    [Test]
    public async Task Test_Start_ShouldDealHoleCardsAndPostBlinds()
    {
        await JoinBothAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PokerPhase.Preflop));
            Assert.That(_game.Players[0].HoleCards, Has.Count.EqualTo(2));
            Assert.That(_game.Players[1].HoleCards, Has.Count.EqualTo(2));
            // Heads-up: the dealer is the small blind, the other the big blind.
            Assert.That(_game.SmallBlindPlayer.RoundBet, Is.EqualTo(_game.SmallBlindAmount));
            Assert.That(_game.BigBlindPlayer.RoundBet, Is.EqualTo(_game.BigBlindAmount));
            Assert.That(_game.TotalPot, Is.EqualTo(_game.SmallBlindAmount + _game.BigBlindAmount));
        }
    }

    [Test]
    public async Task Test_Fold_ShouldAwardPotToLastPlayer_AndSettleBalances()
    {
        await JoinBothAsync();

        // Heads-up: the small blind (player1) acts first preflop and folds.
        var firstToAct = _game.CurrentPlayer;
        var smallBlind = _game.SmallBlindAmount;
        await _game.FoldAsync(firstToAct.User);

        var winnerId = firstToAct.UserId == "player1" ? "player2" : "player1";
        var loserId = firstToAct.UserId;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PokerPhase.Finished));
            Assert.That(_game.WentToShowdown, Is.False);
            // Loser only lost their small blind, winner gained it.
            Assert.That(_moneyService.GetBalance(winnerId), Is.EqualTo(100 + smallBlind));
            Assert.That(_moneyService.GetBalance(loserId), Is.EqualTo(100 - smallBlind));
        }
    }

    [Test]
    public async Task Test_CheckedDownHand_ShouldGoToShowdownAndConserveMoney()
    {
        await JoinBothAsync();

        await PlayCheckOrCallUntilFinishedAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PokerPhase.Finished));
            Assert.That(_game.WentToShowdown, Is.True);
            Assert.That(_game.CommunityCards, Has.Count.EqualTo(5));
            // Money is conserved across the whole hand.
            Assert.That(_moneyService.GetBalance("player1") + _moneyService.GetBalance("player2"), Is.EqualTo(200));
        }
    }

    [Test]
    public async Task Test_Cancel_ShouldRefundEveryPlayer()
    {
        await JoinBothAsync();

        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_moneyService.GetBalance("player1"), Is.EqualTo(100));
            Assert.That(_moneyService.GetBalance("player2"), Is.EqualTo(100));
        }
    }

    [Test]
    public async Task Test_ForFunMode_ShouldSeatBrokePlayers_AndMoveNoMoney()
    {
        _game.IsForFun = true;
        // Both players are broke: in a real-money game they could not buy in.
        _moneyService.Seed(ROOM_ID, "player1", 0);
        _moneyService.Seed(ROOM_ID, "player2", 0);
        var users = new[] { User("player1"), User("player2") };

        foreach (var user in users)
        {
            var (success, _, _) = await _game.JoinAsync(user);
            Assert.That(success, Is.True);
        }

        Assert.That(_game.PlayerCount, Is.EqualTo(2));

        await _game.StartAsync(users[0]);
        var firstToAct = _game.CurrentPlayer;
        await _game.FoldAsync(firstToAct.User);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PokerPhase.Finished));
            // No real bucks were ever taken on buy-in or paid out on settle.
            Assert.That(_moneyService.GetBalance("player1"), Is.EqualTo(0));
            Assert.That(_moneyService.GetBalance("player2"), Is.EqualTo(0));
        }
    }

    /// <summary>
    /// In-memory <see cref="IMoneyService"/> mirroring the real default-100 / get-or-create semantics.
    /// </summary>
    private sealed class FakeMoneyService : IMoneyService
    {
        private readonly Dictionary<(string RoomId, string UserId), long> _balances = new();

        public void Seed(string roomId, string userId, long amount) => _balances[(roomId, userId)] = amount;

        public long GetBalance(string userId) =>
            _balances.TryGetValue((ROOM_ID, userId), out var amount) ? amount : IMoneyService.DEFAULT_BALANCE;

        public Task<long> GetBalanceAsync(string roomId, string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_balances.TryGetValue((roomId, userId), out var amount)
                ? amount
                : IMoneyService.DEFAULT_BALANCE);

        public Task<long> AddAsync(string roomId, string userId, long amount,
            CancellationToken cancellationToken = default)
        {
            var current = _balances.TryGetValue((roomId, userId), out var value)
                ? value
                : IMoneyService.DEFAULT_BALANCE;
            var updated = current + amount;
            _balances[(roomId, userId)] = updated;
            return Task.FromResult(updated);
        }
    }
}
