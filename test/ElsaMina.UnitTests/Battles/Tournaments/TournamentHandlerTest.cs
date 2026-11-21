using ElsaMina.Battles.Tournaments;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Tournaments;

[TestFixture]
public class TournamentHandlerTest
{
    private static readonly string[] HANDLED_MESSAGE_TYPES = ["tournament", "init", "deinit"];

    private ITournamentPlayingService _tournamentPlayingService;
    private TournamentHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _tournamentPlayingService = Substitute.For<ITournamentPlayingService>();
        _handler = new TournamentHandler(_tournamentPlayingService);
    }

    [Test]
    public void Test_HandledMessageTypes_ShouldBeTournamentMessagesAndRoomJoinsAndLeaves()
    {
        // Assert
        Assert.That(_handler.HandledMessageTypes, Is.EquivalentTo(HANDLED_MESSAGE_TYPES));
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldPassMessageToService()
    {
        // Arrange
        string[] parts = ["", "tournament", "updateEnd"];

        // Act
        await _handler.HandleReceivedMessageAsync(parts, "randbats");

        // Assert
        await _tournamentPlayingService.Received(1).HandleMessageAsync(parts, "randbats", Arg.Any<CancellationToken>());
    }
}
