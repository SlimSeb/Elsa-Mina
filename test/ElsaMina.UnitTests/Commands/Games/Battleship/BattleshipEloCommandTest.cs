using ElsaMina.Commands.Games.Battleship;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Battleship;

public class BattleshipEloCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IContext _context;
    private BattleshipEloCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("alice");
        _context = Substitute.For<IContext>();
        _context.Sender.Returns(sender);
        _context.Target.Returns(string.Empty);

        _command = new BattleshipEloCommand(_dbContextFactory);
    }

    private async Task SeedRatingAsync(string userId, int rating, int wins, int losses)
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        dbContext.BattleshipRatings.Add(new BattleshipRating
            { UserId = userId, Rating = rating, Wins = wins, Losses = losses });
        await dbContext.SaveChangesAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenRatingDoesNotExist()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("battleship_elo_not_found", "alice");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySenderRating_WhenTargetIsEmpty()
    {
        await SeedRatingAsync("alice", 1050, 4, 2);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("battleship_elo_info", "alice", 1050, 4, 2);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyTargetRating_WhenTargetIsProvided()
    {
        await SeedRatingAsync("bobsmith", 980, 1, 3);
        _context.Target.Returns(" Bob Smith ");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("battleship_elo_info", "bobsmith", 980, 1, 3);
    }
}
