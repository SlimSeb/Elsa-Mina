using ElsaMina.Battles.Tournaments;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Tournaments;

[TestFixture]
public class JoinTournamentCommandTest
{
    private const string ROOM = "randbats";

    private ITournamentPlayingService _tournamentPlayingService;
    private IContext _context;
    private JoinTournamentCommand _command;

    [SetUp]
    public void SetUp()
    {
        _tournamentPlayingService = Substitute.For<ITournamentPlayingService>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns(ROOM);
        _command = new JoinTournamentCommand(_tournamentPlayingService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.False);
        }
    }

    [TestCase(TournamentJoinResult.Joining, "jointour_joining")]
    [TestCase(TournamentJoinResult.NoTournament, "jointour_no_tournament")]
    [TestCase(TournamentJoinResult.AlreadyJoined, "jointour_already_joined")]
    [TestCase(TournamentJoinResult.AlreadyStarted, "jointour_already_started")]
    public async Task Test_RunAsync_ShouldTellWhatHappened_WhenJoiningRoomTournament(TournamentJoinResult result,
        string expectedKey)
    {
        // Arrange
        _tournamentPlayingService.Join(ROOM).Returns(result);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage(expectedKey);
    }

    [Test]
    public async Task Test_RunAsync_ShouldListSupportedFormats_WhenFormatIsNotSupported()
    {
        // Arrange
        _tournamentPlayingService.Join(ROOM).Returns(TournamentJoinResult.UnsupportedFormat);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("jointour_unsupported_format", "gen9randombattle");
    }
}
