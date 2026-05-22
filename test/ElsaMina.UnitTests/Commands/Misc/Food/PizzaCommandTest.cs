using ElsaMina.Commands.Misc.Food;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Food;

[TestFixture]
public class PizzaCommandTest
{
    private IRandomService _randomService;
    private IContext _context;
    private PizzaCommand _command;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>();
        _context = Substitute.For<IContext>();
        _command = new PizzaCommand(_randomService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBePizzaHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("pizza_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidNumber_WhenTargetIsNotANumber()
    {
        _context.Target.Returns("abc");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pizza_invalid_number");
        _context.DidNotReceive().Reply(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidNumber_WhenTargetIsZero()
    {
        _context.Target.Returns("0");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pizza_invalid_number");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidNumber_WhenTargetIsNegative()
    {
        _context.Target.Returns("-1");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pizza_invalid_number");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyTooManyToppings_WhenCountExceedsItemsLength()
    {
        _context.Target.Returns("9999");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pizza_too_many_toppings");
        _context.DidNotReceive().Reply(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithToppings_WhenCountIsValid()
    {
        _context.Target.Returns("3");
        _randomService.NextDouble().Returns(0.0);

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Is<string>(s => s.Split(", ").Length == 3));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithRandomCount_WhenNoTargetProvided()
    {
        _context.Target.Returns(string.Empty);
        _randomService.NextInt(1, 6).Returns(4);
        _randomService.NextDouble().Returns(0.0);

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Is<string>(s => s.Split(", ").Length == 4));
    }
}
