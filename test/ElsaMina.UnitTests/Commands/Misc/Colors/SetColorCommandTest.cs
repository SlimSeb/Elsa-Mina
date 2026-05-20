using ElsaMina.Commands.Users.Colors;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Users.Colors;

[TestFixture]
public class SetColorCommandTest
{
    private IContext _context;
    private INameColorsService _nameColorsService;
    private SetColorCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _nameColorsService = Substitute.For<INameColorsService>();
        _command = new SetColorCommand(_nameColorsService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeSetcolorHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("setcolor_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetHasNoComma()
    {
        _context.Target.Returns("SomeUserWithoutColor");

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _nameColorsService.DidNotReceive()
            .SetColorAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidHex_WhenColorIsNotAHexCode()
    {
        _context.Target.Returns("SomeUser, notahexcolor");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("setcolor_invalid_hex");
        await _nameColorsService.DidNotReceive()
            .SetColorAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestCase("SomeUser, #FF0000")]
    [TestCase("SomeUser, #abc")]
    [TestCase("SomeUser, #ABCDEF")]
    public async Task Test_RunAsync_ShouldSetColorAndReplySuccess_WhenHexColorIsValid(string target)
    {
        _context.Target.Returns(target);

        await _command.RunAsync(_context);

        await _nameColorsService.Received(1)
            .SetColorAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("setcolor_success", Arg.Any<object[]>());
    }

    [TestCase("SomeUser, #GGG")]
    [TestCase("SomeUser, 123456")]
    [TestCase("SomeUser, #GGGGGG")]
    public async Task Test_RunAsync_ShouldReplyInvalidHex_WhenColorHasWrongFormat(string target)
    {
        _context.Target.Returns(target);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("setcolor_invalid_hex");
        await _nameColorsService.DidNotReceive()
            .SetColorAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
