using ElsaMina.Commands.Alerts;
using ElsaMina.Core.Contexts;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Alerts;

public class AlertsListCommandTest
{
    private IAlertsManager _alertsManager;
    private IContext _context;
    private AlertsListCommand _command;

    [SetUp]
    public void SetUp()
    {
        _alertsManager = Substitute.For<IAlertsManager>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("room");
        _command = new AlertsListCommand(_alertsManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenRoomHasNoAlerts()
    {
        // Arrange
        _alertsManager.GetRoomAlerts("room").Returns([]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_list_empty");
    }

    [Test]
    public async Task Test_RunAsync_ShouldListAlerts_WhenRoomHasAlerts()
    {
        // Arrange
        _alertsManager.GetRoomAlerts("room").Returns([
            new AlertSubscription("room", "twitch", "1", "streamer"),
            new AlertSubscription("room", "youtube", "UC1", "youtuber")
        ]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_list", "streamer (twitch), youtuber (youtube)");
    }
}
