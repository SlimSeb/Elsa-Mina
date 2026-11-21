using ElsaMina.Battles.Tournaments;
using ElsaMina.Core.Contexts;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Tournaments;

[TestFixture]
public class LeaveTournamentCommandTest
{
    private const string ROOM = "randbats";

    private ITournamentPlayingService _tournamentPlayingService;
    private IContext _context;
    private LeaveTournamentCommand _command;

    [SetUp]
    public void SetUp()
    {
        _tournamentPlayingService = Substitute.For<ITournamentPlayingService>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns(ROOM);
        _command = new LeaveTournamentCommand(_tournamentPlayingService);
    }

    [TestCase(true, "leavetour_leaving")]
    [TestCase(false, "leavetour_not_joined")]
    public async Task Test_RunAsync_ShouldTellWhatHappened_WhenLeavingRoomTournament(bool isLeaving,
        string expectedKey)
    {
        // Arrange
        _tournamentPlayingService.Leave(ROOM).Returns(isLeaving);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage(expectedKey);
    }
}
