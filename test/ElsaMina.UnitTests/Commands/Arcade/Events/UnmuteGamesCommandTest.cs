using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Events;

public class UnmuteGamesCommandTest
{
    private IArcadeEventsService _arcadeEventsService;
    private IContext _context;
    private UnmuteGamesCommand _command;

    [SetUp]
    public void SetUp()
    {
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");

        _command = new UnmuteGamesCommand(_arcadeEventsService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUnmuteGamesAndReply()
    {
        await _command.RunAsync(_context);

        _arcadeEventsService.Received(1).UnmuteGames("arcade");
        _context.Received(1).ReplyLocalizedMessage("games_unmuted");
    }
}
