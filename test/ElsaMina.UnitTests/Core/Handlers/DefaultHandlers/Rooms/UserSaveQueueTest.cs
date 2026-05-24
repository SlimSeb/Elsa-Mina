using ElsaMina.Core.Handlers.DefaultHandlers.Rooms;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Handlers.DefaultHandlers.Rooms;

public class UserSaveQueueTests
{
    private IConfiguration _configuration = null!;
    private IBotDbContextFactory _dbContextFactory = null!;
    private DbContextOptions<BotDbContext> _options = null!;
    private IClockService _clockService = null!;

    [SetUp]
    public void SetUp()
    {
        _options = CreateInMemoryOptions();

        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(new DateTimeOffset(2026, 2, 19, 0, 0, 0, TimeSpan.Zero));
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));

        _configuration = Substitute.For<IConfiguration>();
        _configuration.UserUpdateBatchSize.Returns(2);
        _configuration.UserUpdateFlushInterval.Returns(TimeSpan.FromMilliseconds(50));
    }

    [Test]
    public async Task Enqueue_ShouldFlush_WhenBatchSizeReached()
    {
        // Arrange
        var firstSeen = new DateTimeOffset(2026, 2, 19, 1, 0, 0, TimeSpan.Zero);
        var secondSeen = new DateTimeOffset(2026, 2, 19, 1, 5, 0, TimeSpan.Zero);
        _clockService.CurrentUtcDateTimeOffset.Returns(firstSeen, secondSeen);
        var queue = CreateQueue();

        // Act
        queue.Enqueue("AUser", "room1", UserAction.Joining);
        queue.Enqueue("BUser", "room2", UserAction.Leaving); // déclenche le flush
        await queue.WaitForFlushAsync();

        // Assert
        var users = await LoadUsersAsync();

        Assert.That(users, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(users.Select(u => u.UserId),
                Is.EquivalentTo((string[]) ["auser", "buser"]));
            Assert.That(users.Single(u => u.UserId == "auser").LastSeenAction, Is.EqualTo(UserAction.Joining));
            Assert.That(users.Single(u => u.UserId == "auser").LastSeenRoomId, Is.EqualTo("room1"));
            Assert.That(users.Single(u => u.UserId == "auser").LastOnline, Is.EqualTo(firstSeen));
            Assert.That(users.Single(u => u.UserId == "buser").LastSeenAction, Is.EqualTo(UserAction.Leaving));
            Assert.That(users.Single(u => u.UserId == "buser").LastSeenRoomId, Is.EqualTo("room2"));
            Assert.That(users.Single(u => u.UserId == "buser").LastOnline, Is.EqualTo(secondSeen));
        }
    }

    [Test]
    public async Task FlushAsync_ShouldUpdateExistingUser_WhenUserAlreadyExists()
    {
        // Arrange
        await SeedUserAsync(new SavedUser
        {
            UserId = "user1",
            UserName = "OldName"
        });

        var queue = CreateQueue();
        queue.Enqueue(" User 1", "room-a", UserAction.Chatting);

        // Act
        await queue.FlushAsync(CancellationToken.None);

        // Assert
        await using var db = new BotDbContext(_options);
        var user = await db.Users.FindAsync("user1");

        Assert.That(user, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(user!.UserId, Is.EqualTo("user1"));
            Assert.That(user.UserName, Is.EqualTo("User 1"));
            Assert.That(user.LastOnline, Is.EqualTo(_clockService.CurrentUtcDateTimeOffset));
            Assert.That(user.LastSeenRoomId, Is.EqualTo("room-a"));
            Assert.That(user.LastSeenAction, Is.EqualTo(UserAction.Chatting));
        }
    }

    [Test]
    public async Task WaitForFlushAsync_ShouldAwaitFlush_WhenFlushIsInProgress()
    {
        // Arrange
        _dbContextFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(100);
                return new BotDbContext(_options);
            });

        var queue = CreateQueue();
        queue.Enqueue("UserA", "room1", UserAction.Joining);
        queue.Enqueue("UserB", "room1", UserAction.Joining); // déclenche le flush

        // Act
        var waitTask = queue.WaitForFlushAsync();

        // Assert
        Assert.That(waitTask.IsCompleted, Is.False);
        await waitTask;
    }

    [Test]
    public async Task AcquireLockAsync_ShouldBlockFlush()
    {
        // Arrange
        var queue = CreateQueue();
        await queue.AcquireLockAsync();

        // Act
        var flushTask = queue.FlushAsync(CancellationToken.None);
        
        // Assert
        Assert.That(flushTask.IsCompleted, Is.False);
        
        queue.ReleaseLock();
        await flushTask;
        Assert.That(flushTask.IsCompleted, Is.True);
    }

    private UserSaveQueue CreateQueue()
        => new(_dbContextFactory, _configuration, _clockService);

    private static DbContextOptions<BotDbContext> CreateInMemoryOptions()
        => new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private async Task SeedUserAsync(SavedUser user)
    {
        await using var db = new BotDbContext(_options);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private async Task<List<SavedUser>> LoadUsersAsync()
    {
        await using var db = new BotDbContext(_options);
        return await db.Users.AsNoTracking().ToListAsync();
    }
}
