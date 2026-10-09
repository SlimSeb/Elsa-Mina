using ElsaMina.Commands.Economy;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Economy;

public class TransferMoneyCommandTest
{
    private IMoneyService _moneyService;
    private IContext _context;
    private IRoom _room;
    private TransferMoneyCommand _command;

    [SetUp]
    public void SetUp()
    {
        _moneyService = Substitute.For<IMoneyService>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("alice");
        _context.Sender.Returns(sender);
        _context.Room.Returns(_room);
        _context.RoomId.Returns("room");
        _room.GetParameterValueAsync(EconomyRoomParameters.BucksEnabled, Arg.Any<CancellationToken>()).Returns("true");

        _command = new TransferMoneyCommand(_moneyService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyBucksDisabled_WhenBucksAreDisabled()
    {
        _room.GetParameterValueAsync(EconomyRoomParameters.BucksEnabled, Arg.Any<CancellationToken>()).Returns("false");
        _context.Target.Returns("bob, 10");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("bucks_disabled");
        await _moneyService.DidNotReceiveWithAnyArgs().AddAsync(default, default, default);
    }

    [TestCase("bob")]
    [TestCase("bob, 10, extra")]
    [TestCase("!!!, 10")]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenArgumentsAreInvalid(string target)
    {
        _context.Target.Returns(target);

        await _command.RunAsync(_context);

        _context.Received(1).GetString("transfer_money_help");
        await _moneyService.DidNotReceiveWithAnyArgs().AddAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySelfTransferError_WhenRecipientIsSender()
    {
        _context.Target.Returns("Alice, 10");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("transfer_money_self");
        await _moneyService.DidNotReceiveWithAnyArgs().AddAsync(default, default, default);
    }

    [TestCase("bob, abc")]
    [TestCase("bob, 0")]
    [TestCase("bob, -5")]
    public async Task Test_RunAsync_ShouldReplyInvalidAmount_WhenAmountIsNotStrictlyPositive(string target)
    {
        _context.Target.Returns(target);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("money_invalid_amount");
        await _moneyService.DidNotReceiveWithAnyArgs().AddAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInsufficientFunds_WhenSenderBalanceIsTooLow()
    {
        _context.Target.Returns("bob, 50");
        _moneyService.GetBalanceAsync("room", "alice", Arg.Any<CancellationToken>()).Returns(20L);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("transfer_money_insufficient_funds", 20L);
        await _moneyService.DidNotReceiveWithAnyArgs().AddAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldMoveMoneyAndReplySuccess_WhenSenderHasEnoughFunds()
    {
        _context.Target.Returns("Bob, 50");
        _moneyService.GetBalanceAsync("room", "alice", Arg.Any<CancellationToken>()).Returns(80L);
        _moneyService.AddAsync("room", "alice", -50, Arg.Any<CancellationToken>()).Returns(30L);

        await _command.RunAsync(_context);

        await _moneyService.Received(1).AddAsync("room", "alice", -50, Arg.Any<CancellationToken>());
        await _moneyService.Received(1).AddAsync("room", "bob", 50, Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("transfer_money_success", 50L, "Bob", 30L);
    }

    [Test]
    public async Task Test_RunAsync_ShouldAllowTransfer_WhenAmountEqualsSenderBalance()
    {
        _context.Target.Returns("bob, 80");
        _moneyService.GetBalanceAsync("room", "alice", Arg.Any<CancellationToken>()).Returns(80L);

        await _command.RunAsync(_context);

        await _moneyService.Received(1).AddAsync("room", "bob", 80, Arg.Any<CancellationToken>());
    }
}
