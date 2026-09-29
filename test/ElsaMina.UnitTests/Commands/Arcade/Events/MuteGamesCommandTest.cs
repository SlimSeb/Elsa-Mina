using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Events;

public class MuteGamesCommandTest
{
    private IArcadeEventsService _arcadeEventsService;
    private IContext _context;
    private MuteGamesCommand _command;

    [SetUp]
    public void SetUp()
    {
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");

        _command = new MuteGamesCommand(_arcadeEventsService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("abc")]
    [TestCase("0")]
    [TestCase("-10")]
    public async Task Test_RunAsync_ShouldMuteForDefaultDuration_WhenTargetIsNotAPositiveNumber(string target)
    {
        _context.Target.Returns(target);

        await _command.RunAsync(_context);

        _arcadeEventsService.Received(1).MuteGames("arcade", TimeSpan.FromMinutes(30));
        _context.Received(1).ReplyLocalizedMessage("games_muted", 30);
    }

    [Test]
    public async Task Test_RunAsync_ShouldMuteForGivenDuration_WhenTargetIsAPositiveNumber()
    {
        _context.Target.Returns(" 45 ");

        await _command.RunAsync(_context);

        _arcadeEventsService.Received(1).MuteGames("arcade", TimeSpan.FromMinutes(45));
        _context.Received(1).ReplyLocalizedMessage("games_muted", 45);
    }
}
