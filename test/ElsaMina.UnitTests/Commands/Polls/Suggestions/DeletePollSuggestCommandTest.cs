using ElsaMina.Commands.Polls.Suggestions;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Polls.Suggestions;

[TestFixture]
public class DeletePollSuggestCommandTest
{
    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _dbContextFactory;
    private IContext _context;
    private DeletePollSuggestCommand _command;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));

        _context = Substitute.For<IContext>();

        _command = new DeletePollSuggestCommand(_dbContextFactory);
    }

    private async Task<int> SeedSuggestionAsync()
    {
        await using var db = new BotDbContext(_options);
        var suggestion = new PollSuggestion
        {
            RoomId = "testroom",
            UserId = "testuser",
            UserName = "TestUser",
            Content = "My poll",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.PollSuggestions.Add(suggestion);
        await db.SaveChangesAsync();
        return suggestion.Id;
    }

    private async Task<int> CountSuggestionsAsync()
    {
        await using var db = new BotDbContext(_options);
        return await db.PollSuggestions.CountAsync();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeDeletesuggpollHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("deletesuggpoll_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsNotAnInteger()
    {
        _context.Target.Returns("notanumber");

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsNull()
    {
        _context.Target.Returns((string)null);

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenSuggestionDoesNotExist()
    {
        _context.Target.Returns("999");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("deletesuggpoll_not_found", 999);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteSuggestion_WhenSuggestionExists()
    {
        var id = await SeedSuggestionAsync();
        _context.Target.Returns(id.ToString());

        await _command.RunAsync(_context);

        Assert.That(await CountSuggestionsAsync(), Is.Zero);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySuccess_WhenSuggestionIsDeleted()
    {
        var id = await SeedSuggestionAsync();
        _context.Target.Returns(id.ToString());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("deletesuggpoll_success", id);
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimTarget_WhenTargetHasWhitespace()
    {
        var id = await SeedSuggestionAsync();
        _context.Target.Returns($"  {id}  ");

        await _command.RunAsync(_context);

        Assert.That(await CountSuggestionsAsync(), Is.Zero);
    }
}
