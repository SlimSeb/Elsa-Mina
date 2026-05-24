using ElsaMina.Commands.Polls.Suggestions;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Polls.Suggestions;

[TestFixture]
public class BanPollCommandTest
{
    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _dbContextFactory;
    private IConfiguration _configuration;
    private IContext _context;
    private IUser _sender;
    private BanPollCommand _command;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));

        _configuration = Substitute.For<IConfiguration>();
        _configuration.DefaultRoom.Returns("testroom");

        _sender = Substitute.For<IUser>();
        _sender.Name.Returns("Moderator");

        _context = Substitute.For<IContext>();
        _context.Sender.Returns(_sender);

        _command = new BanPollCommand(_dbContextFactory, _configuration);
    }

    private async Task SeedBanAsync(string userId, string roomId)
    {
        await using var db = new BotDbContext(_options);
        db.PollSuggestionBans.Add(new PollSuggestionBan { UserId = userId, RoomId = roomId });
        await db.SaveChangesAsync();
    }

    private async Task<int> CountBansAsync()
    {
        await using var db = new BotDbContext(_options);
        return await db.PollSuggestionBans.CountAsync();
    }

    private async Task<PollSuggestionBan> GetFirstBanAsync()
    {
        await using var db = new BotDbContext(_options);
        return await db.PollSuggestionBans.FirstOrDefaultAsync();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeBanpollHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("banpoll_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsEmpty()
    {
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsWhitespace()
    {
        _context.Target.Returns("   ");

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyBanned_WhenUserIsAlreadyBanned()
    {
        await SeedBanAsync("baduser", "testroom");
        _context.Target.Returns("BadUser");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("banpoll_already_banned", "baduser");
        Assert.That(await CountBansAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Test_RunAsync_ShouldSaveBan_WhenUserIsNotBanned()
    {
        _context.Target.Returns("BadUser");

        await _command.RunAsync(_context);

        var ban = await GetFirstBanAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ban, Is.Not.Null);
            Assert.That(ban.UserId, Is.EqualTo("baduser"));
            Assert.That(ban.RoomId, Is.EqualTo("testroom"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySuccess_WhenBanIsSaved()
    {
        _context.Target.Returns("BadUser");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("banpoll_success", "baduser");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendMessageToStaffRoom_WhenBanIsSaved()
    {
        _context.Target.Returns("BadUser");

        await _command.RunAsync(_context);

        _context.Received(1).SendMessageIn("frenchstaff", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeUserId_WhenTargetHasSpecialChars()
    {
        _context.Target.Returns("  Bad User!  ");

        await _command.RunAsync(_context);

        var ban = await GetFirstBanAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ban, Is.Not.Null);
            Assert.That(ban.UserId, Is.EqualTo("baduser"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotSaveBan_WhenUserAlreadyBannedInSameRoom()
    {
        await SeedBanAsync("baduser", "testroom");
        _context.Target.Returns("BadUser");

        await _command.RunAsync(_context);

        Assert.That(await CountBansAsync(), Is.EqualTo(1));
    }
}
