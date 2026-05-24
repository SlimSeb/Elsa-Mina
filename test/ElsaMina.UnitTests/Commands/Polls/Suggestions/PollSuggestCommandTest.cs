using ElsaMina.Commands.Polls.Suggestions;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Polls.Suggestions;

[TestFixture]
public class PollSuggestCommandTest
{
    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _dbContextFactory;
    private IConfiguration _configuration;
    private IClockService _clockService;
    private IContext _context;
    private IUser _sender;
    private PollSuggestCommand _command;

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
        _configuration.Trigger.Returns("-");

        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);

        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("testuser");
        _sender.Name.Returns("TestUser");

        _context = Substitute.For<IContext>();
        _context.Sender.Returns(_sender);

        _command = new PollSuggestCommand(_dbContextFactory, _configuration, _clockService);
    }

    private async Task SeedBanAsync(string userId, string roomId)
    {
        await using var db = new BotDbContext(_options);
        db.PollSuggestionBans.Add(new PollSuggestionBan { UserId = userId, RoomId = roomId });
        await db.SaveChangesAsync();
    }

    private async Task<PollSuggestion> GetFirstSuggestionAsync()
    {
        await using var db = new BotDbContext(_options);
        return await db.PollSuggestions.FirstOrDefaultAsync();
    }

    private async Task<int> CountSuggestionsAsync()
    {
        await using var db = new BotDbContext(_options);
        return await db.PollSuggestions.CountAsync();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBePollsuggestHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("pollsuggest_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsNull()
    {
        _context.Target.Returns((string)null);

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
    public async Task Test_RunAsync_ShouldReplyBanned_WhenUserIsBanned()
    {
        await SeedBanAsync("testuser", "testroom");
        _context.Target.Returns("My poll suggestion");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pollsuggest_banned");
        Assert.That(await CountSuggestionsAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldSaveSuggestion_WhenUserIsNotBanned()
    {
        _context.Target.Returns("My poll suggestion");

        await _command.RunAsync(_context);

        var saved = await GetFirstSuggestionAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.RoomId, Is.EqualTo("testroom"));
            Assert.That(saved.UserId, Is.EqualTo("testuser"));
            Assert.That(saved.UserName, Is.EqualTo("TestUser"));
            Assert.That(saved.Content, Is.EqualTo("My poll suggestion"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimSuggestion_WhenTargetHasWhitespace()
    {
        _context.Target.Returns("  My poll suggestion  ");

        await _command.RunAsync(_context);

        var saved = await GetFirstSuggestionAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.Content, Is.EqualTo("My poll suggestion"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySuccess_WhenSuggestionIsSaved()
    {
        _context.Target.Returns("My poll suggestion");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pollsuggest_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendMessageToStaffRoom_WhenSuggestionIsSaved()
    {
        _context.Target.Returns("My poll suggestion");

        await _command.RunAsync(_context);

        _context.Received(1).SendMessageIn("frenchstaff", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotReplyBanned_WhenUserBanIsForDifferentRoom()
    {
        await SeedBanAsync("testuser", "otherroom");
        _context.Target.Returns("My poll suggestion");

        await _command.RunAsync(_context);

        _context.DidNotReceive().ReplyLocalizedMessage("pollsuggest_banned");
        _context.Received(1).ReplyLocalizedMessage("pollsuggest_success");
    }
}
