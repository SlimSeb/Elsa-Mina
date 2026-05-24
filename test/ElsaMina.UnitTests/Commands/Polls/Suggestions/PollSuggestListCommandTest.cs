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
public class PollSuggestListCommandTest
{
    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _dbContextFactory;
    private IConfiguration _configuration;
    private IContext _context;
    private PollSuggestListCommand _command;

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

        _context = Substitute.For<IContext>();

        _command = new PollSuggestListCommand(_dbContextFactory, _configuration);
    }

    private async Task SeedSuggestionAsync(string content, string userId = "user1", string userName = "User1", string roomId = "testroom")
    {
        await using var db = new BotDbContext(_options);
        db.PollSuggestions.Add(new PollSuggestion
        {
            RoomId = roomId,
            UserId = userId,
            UserName = userName,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenNoSuggestionsExist()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pollsuggestlist_empty");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenSuggestionsExistForOtherRoomOnly()
    {
        await SeedSuggestionAsync("Poll from other room", roomId: "otherroom");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pollsuggestlist_empty");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WhenSuggestionsExist()
    {
        await SeedSuggestionAsync("My poll");

        await _command.RunAsync(_context);

        _context.DidNotReceive().ReplyLocalizedMessage("pollsuggestlist_empty");
        _context.Received(1).ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeSuggestionContentInHtml_WhenSuggestionsExist()
    {
        await SeedSuggestionAsync("Best poll ever", userName: "User1");

        string capturedHtml = null;
        _context.When(c => c.ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(call => capturedHtml = call.ArgAt<string>(0));

        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedHtml, Does.Contain("Best poll ever"));
            Assert.That(capturedHtml, Does.Contain("User1"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeDeleteButton_WhenSuggestionsExist()
    {
        await SeedSuggestionAsync("My poll");

        string capturedHtml = null;
        _context.When(c => c.ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(call => capturedHtml = call.ArgAt<string>(0));

        await _command.RunAsync(_context);

        Assert.That(capturedHtml, Does.Contain("-deletesuggpoll"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderSuggestionsByIdDescending_WhenMultipleSuggestionsExist()
    {
        await SeedSuggestionAsync("First");
        await SeedSuggestionAsync("Second");

        string capturedHtml = null;
        _context.When(c => c.ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(call => capturedHtml = call.ArgAt<string>(0));

        await _command.RunAsync(_context);

        Assert.That(capturedHtml, Is.Not.Null);
        var firstIndex = capturedHtml.IndexOf("First", StringComparison.Ordinal);
        var secondIndex = capturedHtml.IndexOf("Second", StringComparison.Ordinal);
        Assert.That(secondIndex, Is.LessThan(firstIndex));
    }
}
