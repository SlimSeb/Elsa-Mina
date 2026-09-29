using ElsaMina.Commands.Economy;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Economy;

public class MoneyCommandTest
{
    private IMoneyService _moneyService;
    private IContext _context;
    private IRoom _room;
    private MoneyCommand _command;

    [SetUp]
    public void SetUp()
    {
        _moneyService = Substitute.For<IMoneyService>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        var sender = Substitute.For<IUser>();
        sender.Name.Returns("Alice");
        _context.Sender.Returns(sender);
        _context.Room.Returns(_room);
        _context.RoomId.Returns("room");
        _context.Target.Returns(string.Empty);
        _room.GetParameterValueAsync(Parameter.BucksEnabled, Arg.Any<CancellationToken>()).Returns("true");

        _command = new MoneyCommand(_moneyService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyBucksDisabled_WhenBucksAreDisabled()
    {
        _room.GetParameterValueAsync(Parameter.BucksEnabled, Arg.Any<CancellationToken>()).Returns("false");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("bucks_disabled");
        await _moneyService.DidNotReceiveWithAnyArgs().GetBalanceAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowSenderBalance_WhenTargetIsEmpty()
    {
        _moneyService.GetBalanceAsync("room", "alice", Arg.Any<CancellationToken>()).Returns(250L);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("money_balance", "Alice", 250L);
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowTargetBalance_WhenTargetIsProvided()
    {
        _context.Target.Returns(" Bob Smith ");
        _moneyService.GetBalanceAsync("room", "bobsmith", Arg.Any<CancellationToken>()).Returns(42L);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("money_balance", "Bob Smith", 42L);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetHasNoAlphanumericCharacters()
    {
        _context.Target.Returns("!!!");

        await _command.RunAsync(_context);

        _context.Received(1).GetString("money_help");
        await _moneyService.DidNotReceiveWithAnyArgs().GetBalanceAsync(default, default);
    }
}
