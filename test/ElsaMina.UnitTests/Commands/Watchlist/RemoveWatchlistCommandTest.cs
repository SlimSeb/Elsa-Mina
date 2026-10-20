using ElsaMina.Commands.Watchlist;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Watchlist;

public class RemoveWatchlistCommandTest
{
    private IWatchlistService _watchlistService;
    private IContext _context;
    private RemoveWatchlistCommand _command;

    [SetUp]
    public void SetUp()
    {
        _watchlistService = Substitute.For<IWatchlistService>();
        _context = Substitute.For<IContext>();

        var sender = Substitute.For<IUser>();
        sender.Name.Returns("Staff");
        _context.Sender.Returns(sender);

        _command = new RemoveWatchlistCommand(_watchlistService);
    }

    [TestCase("")]
    [TestCase("room, alice")]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenArgumentsAreMissing(string target)
    {
        _context.Target.Returns(target);

        await _command.RunAsync(_context);

        _context.Received(1).GetString("watchlist_remove_help");
        await _watchlistService.DidNotReceiveWithAnyArgs().RemoveFromWatchlistAsync(default, default, default);
    }

    [Test]
    [TestCase("room, alice, %", ExpectedResult = "room")]
    public string Test_GetTargetRoomId_ShouldReturnRoomTheCommandActsOn(string target)
    {
        // Arrange
        _context.Target.Returns(target);

        // Act & Assert
        return _command.GetTargetRoomId(_context);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenEntryCannotBeRemoved()
    {
        _context.Target.Returns("room, alice, %");
        _watchlistService.RemoveFromWatchlistAsync("room", "alice", "%", Arg.Any<CancellationToken>())
            .Returns(false);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("watchlist_user_not_found", "alice", "%", "room");
        await _watchlistService.DidNotReceiveWithAnyArgs().SendDiscordNotificationAsync(default, default);
        await _watchlistService.DidNotReceiveWithAnyArgs().FetchAndUpdateStaffIntroAsync(default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRemovedNotifyAndUpdateStaffIntro_WhenEntryIsRemoved()
    {
        _context.Target.Returns("room, alice, %");
        _watchlistService.RemoveFromWatchlistAsync("room", "alice", "%", Arg.Any<CancellationToken>())
            .Returns(true);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("watchlist_user_removed", "alice", "%", "room");
        await _watchlistService.Received(1).SendDiscordNotificationAsync("room",
            Arg.Is<string>(message => message.Contains("Staff") && message.Contains("alice")),
            Arg.Any<CancellationToken>());
        await _watchlistService.Received(1).FetchAndUpdateStaffIntroAsync("room", Arg.Any<CancellationToken>());
    }
}
