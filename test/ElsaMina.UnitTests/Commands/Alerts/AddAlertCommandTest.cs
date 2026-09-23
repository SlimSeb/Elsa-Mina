using ElsaMina.Commands.Alerts;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Alerts;

public class AddAlertCommandTest
{
    private IAlertsManager _alertsManager;
    private IAlertChannelResolver _twitchResolver;
    private IContext _context;
    private AddAlertCommand _command;

    [SetUp]
    public void SetUp()
    {
        _alertsManager = Substitute.For<IAlertsManager>();
        _twitchResolver = Substitute.For<IAlertChannelResolver>();
        _twitchResolver.CanResolve(AlertPlatforms.TWITCH).Returns(true);
        _twitchResolver.IsConfigured.Returns(true);
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("room");
        _command = new AddAlertCommand(_alertsManager, [_twitchResolver]);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenArgumentsAreMissing()
    {
        // Arrange
        _context.Target.Returns("twitch");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("alerts_add_help");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUnsupported_WhenPlatformIsUnknown()
    {
        // Arrange
        _context.Target.Returns("tiktok someone");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_unsupported_platform", "tiktok", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotConfigured_WhenNoResolverIsConfigured()
    {
        // Arrange
        _context.Target.Returns("twitter someone");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_platform_not_configured", AlertPlatforms.TWITTER);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenChannelDoesNotExist()
    {
        // Arrange
        _context.Target.Returns("twitch nobody");
        _twitchResolver.ResolveChannelAsync("nobody", Arg.Any<CancellationToken>()).Returns((AlertChannel)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_channel_not_found", "nobody", AlertPlatforms.TWITCH);
        await _alertsManager.DidNotReceiveWithAnyArgs().AddAlertAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenResolverThrows()
    {
        // Arrange
        _context.Target.Returns("twitch someone");
        _twitchResolver.ResolveChannelAsync("someone", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_platform_error", AlertPlatforms.TWITCH);
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddAlert_WhenChannelExists()
    {
        // Arrange
        var channel = new AlertChannel("42", "someone");
        _context.Target.Returns("Twitch someone");
        _twitchResolver.ResolveChannelAsync("someone", Arg.Any<CancellationToken>()).Returns(channel);
        _alertsManager.AddAlertAsync("room", AlertPlatforms.TWITCH, channel, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_added", "someone", AlertPlatforms.TWITCH);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyExists_WhenAlertIsAlreadyRegistered()
    {
        // Arrange
        var channel = new AlertChannel("42", "someone");
        _context.Target.Returns("twitch someone");
        _twitchResolver.ResolveChannelAsync("someone", Arg.Any<CancellationToken>()).Returns(channel);
        _alertsManager.AddAlertAsync("room", AlertPlatforms.TWITCH, channel, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("alerts_already_exists", "someone", AlertPlatforms.TWITCH);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }
}
