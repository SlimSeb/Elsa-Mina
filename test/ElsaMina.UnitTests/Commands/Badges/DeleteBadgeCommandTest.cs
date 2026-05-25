using ElsaMina.Commands.Badges;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace ElsaMina.UnitTests.Commands.Badges;

[TestFixture]
public class DeleteBadgeCommandTest
{
    private IContext _context;
    private IBotDbContextFactory _factory;
    private DeleteBadgeCommand _command;
    private DbContextOptions<BotDbContext> _dbOptions;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _context = Substitute.For<IContext>();
        _factory = Substitute.For<IBotDbContextFactory>();
        
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _command = new DeleteBadgeCommand(_factory);
    }

    [Test]
    public void RequiredRank_ShouldBeDriver()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task RunAsync_ShouldReplyDoesntExist_WhenBadgeNotFound()
    {
        // Arrange
        _context.Target.Returns("NonExistingBadge");
        _context.RoomId.Returns("room1");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_delete_doesnt_exist", "nonexistingbadge");
    }

    [Test]
    public async Task RunAsync_ShouldDeleteBadgeAndReplySuccess_WhenBadgeExists()
    {
        // Arrange
        var badgeId = "existingbadge";
        var roomId = "room1";

        using (var seedContext = new BotDbContext(_dbOptions))
        {
            seedContext.Badges.Add(new Badge { Id = badgeId, RoomId = roomId });
            await seedContext.SaveChangesAsync();
        }

        _context.Target.Returns(badgeId);
        _context.RoomId.Returns(roomId);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_delete_success", badgeId);

        using (var verifyContext = new BotDbContext(_dbOptions))
        {
            var exists = await verifyContext.Badges.AnyAsync(b => b.Id == badgeId && b.RoomId == roomId);
            Assert.That(exists, Is.False);
        }
    }

    [Test]
    public async Task RunAsync_ShouldNormalizeId_AndDeleteCorrectBadge()
    {
        // Arrange
        var rawTarget = "My-Badge_123!";
        var normalizedId = "mybadge123"; 

        using (var seedContext = new BotDbContext(_dbOptions))
        {
            seedContext.Badges.Add(new Badge { Id = normalizedId, RoomId = "room1" });
            await seedContext.SaveChangesAsync();
        }

        _context.Target.Returns(rawTarget);
        _context.RoomId.Returns("room1");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_delete_success", normalizedId);

        using (var verifyContext = new BotDbContext(_dbOptions))
        {
            Assert.That(await verifyContext.Badges.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task RunAsync_ShouldOnlyDeleteBadgeInContextRoom()
    {
        // Arrange
        var badgeId = "sharedbadge";
        
        using (var seedContext = new BotDbContext(_dbOptions))
        {
            seedContext.Badges.Add(new Badge { Id = badgeId, RoomId = "room1" });
            seedContext.Badges.Add(new Badge { Id = badgeId, RoomId = "room2" });
            await seedContext.SaveChangesAsync();
        }

        _context.Target.Returns(badgeId);
        _context.RoomId.Returns("room1");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var verifyContext = new BotDbContext(_dbOptions))
        {
            var room1Badge = await verifyContext.Badges.FindAsync(badgeId, "room1");
            var room2Badge = await verifyContext.Badges.FindAsync(badgeId, "room2");

            Assert.IsNull(room1Badge);
            Assert.IsNotNull(room2Badge);
        }
    }

    [Test]
    public async Task RunAsync_ShouldCatchException_AndReplyFailure()
    {
        // Arrange
        _context.Target.Returns("badge");
        
        _factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Throws(new Exception("Database connection failed"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_delete_failure", "Database connection failed");
    }
}