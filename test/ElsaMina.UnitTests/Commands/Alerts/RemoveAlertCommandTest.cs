using ElsaMina.Commands.Alerts;
using ElsaMina.Core.Contexts;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Alerts;

public class RemoveAlertCommandTest
{
    private IAlertsManager _alertsManager;
    private IContext _context;
    private RemoveAlertCommand _command;

    [SetUp]
    public void SetUp()
    {
        _alertsManager = Substitute.For<IAlertsManager>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("room");
        _command = new RemoveAlertCommand(_alertsManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenArgumentsAreMissing()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("alerts_remove_help");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRemoved_WhenAlertExists()
    {
        // Arrange
        _context.Target.Returns("x someone");
        _alertsManager.RemoveAlertAsync("room", AlertPlatforms.TWITTER, "someone", Arg.Any<CancellationToken>())
            .Returns(new AlertSubscription("room", AlertPlatforms.TWITTER, "42", "SomeOne"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_removed", "SomeOne", AlertPlatforms.TWITTER);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenAlertDoesNotExist()
    {
        // Arrange
        _context.Target.Returns("twitch someone");
        _alertsManager.RemoveAlertAsync("room", AlertPlatforms.TWITCH, "someone", Arg.Any<CancellationToken>())
            .Returns((AlertSubscription)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_not_found", "someone", AlertPlatforms.TWITCH);
    }
}
