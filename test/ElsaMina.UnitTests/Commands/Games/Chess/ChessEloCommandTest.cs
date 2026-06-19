using ElsaMina.Commands.Games.Chess;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Chess;

public class ChessEloCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IContext _context;
    private IUser _sender;
    private ChessEloCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => new BotDbContext(_dbOptions));

        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("alice");

        _context = Substitute.For<IContext>();
        _context.Sender.Returns(_sender);
        _context.Target.Returns(string.Empty);

        _command = new ChessEloCommand(_dbContextFactory);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenRatingDoesNotExist()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("chess_elo_not_found", "alice");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseSenderId_WhenTargetIsEmpty()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.ChessRatings.Add(new ChessRating
            {
                UserId = "alice", Rating = 1100, Wins = 5, Losses = 3, Draws = 1
            });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("chess_elo_info", "alice", 1100, 5, 3, 1);
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeTargetId_WhenTargetHasSpacesAndUppercase()
    {
        _context.Target.Returns("  Bob Smith  ");

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.ChessRatings.Add(new ChessRating
            {
                UserId = "bobsmith", Rating = 1000, Wins = 0, Losses = 0, Draws = 0
            });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("chess_elo_info", "bobsmith", 1000, 0, 0, 0);
    }
}
