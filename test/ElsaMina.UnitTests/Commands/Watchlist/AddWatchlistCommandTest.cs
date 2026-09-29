using ElsaMina.Commands.Watchlist;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Watchlist;

public class AddWatchlistCommandTest
{
    private IWatchlistService _watchlistService;
    private IContext _context;
    private AddWatchlistCommand _command;

    [SetUp]
    public void SetUp()
    {
        _watchlistService = Substitute.For<IWatchlistService>();
        _context = Substitute.For<IContext>();

        var sender = Substitute.For<IUser>();
        sender.Name.Returns("Staff");
        _context.Sender.Returns(sender);
        _context.HasSufficientRankInRoom(Arg.Any<string>(), Rank.Driver, Arg.Any<CancellationToken>())
            .Returns(true);

        _command = new AddWatchlistCommand(_watchlistService);
    }

    [Test]
    public void Test_Properties_ShouldBeConfiguredForDriversInPrivateMessages()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
        }
    }

    [TestCase("")]
    [TestCase("room")]
    [TestCase("room, alice")]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenArgumentsAreMissing(string target)
    {
        _context.Target.Returns(target);

        await _command.RunAsync(_context);

        _context.Received(1).GetString("watchlist_add_help");
        await _watchlistService.DidNotReceiveWithAnyArgs().AddToWatchlistAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenSenderHasInsufficientRankInTargetRoom()
    {
        _context.Target.Returns("room, alice, %");
        _context.HasSufficientRankInRoom("room", Rank.Driver, Arg.Any<CancellationToken>()).Returns(false);

        await _command.RunAsync(_context);

        await _watchlistService.DidNotReceiveWithAnyArgs().AddToWatchlistAsync(default, default, default);
        _context.DidNotReceiveWithAnyArgs().ReplyLocalizedMessage(default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddUserNotifyAndUpdateStaffIntro_WhenArgumentsAreValid()
    {
        _context.Target.Returns(" room , alice , % ");

        await _command.RunAsync(_context);

        await _watchlistService.Received(1).AddToWatchlistAsync("room", "alice", "%", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("watchlist_user_added", "alice", "room", "%");
        await _watchlistService.Received(1).SendDiscordNotificationAsync("room",
            Arg.Is<string>(message => message.Contains("Staff") && message.Contains("alice")),
            Arg.Any<CancellationToken>());
        await _watchlistService.Received(1).FetchAndUpdateStaffIntroAsync("room", Arg.Any<CancellationToken>());
    }
}
