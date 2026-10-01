using ElsaMina.Commands.Economy;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Economy;

public class GiveMoneyCommandTest
{
    private IMoneyService _moneyService;
    private IContext _context;
    private IRoom _room;
    private GiveMoneyCommand _command;

    [SetUp]
    public void SetUp()
    {
        _moneyService = Substitute.For<IMoneyService>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        _context.Room.Returns(_room);
        _context.RoomId.Returns("room");
        _room.GetParameterValueAsync(EconomyRoomParameters.BucksEnabled, Arg.Any<CancellationToken>()).Returns("true");

        _command = new GiveMoneyCommand(_moneyService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeAdmin()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Admin));
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

        _context.Received(1).GetString("give_money_help");
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
    public async Task Test_RunAsync_ShouldAddMoneyAndReplySuccess_WhenArgumentsAreValid()
    {
        _context.Target.Returns(" Bob , 25 ");
        _moneyService.AddAsync("room", "bob", 25, Arg.Any<CancellationToken>()).Returns(125L);

        await _command.RunAsync(_context);

        await _moneyService.Received(1).AddAsync("room", "bob", 25, Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("give_money_success", 25L, "Bob", 125L);
    }
}
