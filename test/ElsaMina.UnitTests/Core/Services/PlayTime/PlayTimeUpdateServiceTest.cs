using ElsaMina.Core.Handlers.DefaultHandlers.Rooms;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.PlayTime;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.PlayTime;

public class PlayTimeUpdateServiceTest
{
    private PlayTimeUpdateService _sut;
    private IConfiguration _configuration;
    private IBotDbContextFactory _dbContextFactory;
    private IUserSaveQueue _userSaveQueue;
    private IRoomsManager _roomsManager;
    private DbContextOptions<BotDbContext> _dbContextOptions;

    private IBotDbContextFactory CreateFactory(DbContextOptions<BotDbContext> options)
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(options)));
        return factory;
    }

    [SetUp]
    public void SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _configuration = Substitute.For<IConfiguration>();
        _configuration.PlayTimeUpdatesInterval.Returns(TimeSpan.FromDays(1));

        _dbContextFactory = CreateFactory(_dbContextOptions);
        _userSaveQueue = Substitute.For<IUserSaveQueue>();
        _userSaveQueue.AcquireLockAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.Rooms.Returns([]);

        _sut = new PlayTimeUpdateService(_configuration, _dbContextFactory, _userSaveQueue, _roomsManager);
    }

    [TearDown]
    public void TearDown()
    {
        _sut.Dispose();
    }

    [Test]
    public async Task Test_ProcessPendingPlayTimeUpdatesAsync_ShouldAcquireAndReleaseLock()
    {
        // Act
        await _sut.ProcessPendingPlayTimeUpdatesAsync();

        // Assert
        await _userSaveQueue.Received(1).AcquireLockAsync(Arg.Any<CancellationToken>());
        _userSaveQueue.Received(1).ReleaseLock();
    }

    [Test]
    public async Task Test_ProcessPendingPlayTimeUpdatesAsync_WhenUserDoesNotExist_ShouldCreateUserAndRoomUser()
    {
        // Arrange
        const string roomId = "testroom";
        const string userId = "earth";
        var pendingUpdates = new Dictionary<string, TimeSpan> { [userId] = TimeSpan.FromMinutes(10) };
        var room = BuildRoomSubstitute(roomId, pendingUpdates);
        _roomsManager.Rooms.Returns([room]);

        // Act
        await _sut.ProcessPendingPlayTimeUpdatesAsync();

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        var roomUser = await assertContext.RoomUsers.SingleOrDefaultAsync(u => u.Id == userId && u.RoomId == roomId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(roomUser, Is.Not.Null);
            Assert.That(roomUser.PlayTime, Is.EqualTo(TimeSpan.FromMinutes(10)));
        }
    }

    [Test]
    public async Task Test_ProcessPendingPlayTimeUpdatesAsync_WhenRoomUserAlreadyExists_ShouldIncrementPlayTime()
    {
        // Arrange
        const string roomId = "testroom";
        const string userId = "earth";
        await using (var seedContext = new BotDbContext(_dbContextOptions))
        {
            seedContext.Users.Add(new SavedUser { UserId = userId, UserName = userId });
            seedContext.RoomUsers.Add(new RoomUser { Id = userId, RoomId = roomId, PlayTime = TimeSpan.FromMinutes(20) });
            await seedContext.SaveChangesAsync();
        }

        var pendingUpdates = new Dictionary<string, TimeSpan> { [userId] = TimeSpan.FromMinutes(5) };
        var room = BuildRoomSubstitute(roomId, pendingUpdates);
        _roomsManager.Rooms.Returns([room]);

        // Act
        await _sut.ProcessPendingPlayTimeUpdatesAsync();

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        var roomUser = await assertContext.RoomUsers.SingleOrDefaultAsync(u => u.Id == userId && u.RoomId == roomId);

        Assert.That(roomUser.PlayTime, Is.EqualTo(TimeSpan.FromMinutes(25)));
    }

    [Test]
    public async Task Test_ProcessPendingPlayTimeUpdatesAsync_ShouldClearPendingUpdatesAfterProcessing()
    {
        // Arrange
        const string roomId = "testroom";
        const string userId = "earth";
        var pendingUpdates = new Dictionary<string, TimeSpan> { [userId] = TimeSpan.FromMinutes(10) };
        var room = BuildRoomSubstitute(roomId, pendingUpdates);
        _roomsManager.Rooms.Returns([room]);

        // Act
        await _sut.ProcessPendingPlayTimeUpdatesAsync();

        // Assert
        Assert.That(pendingUpdates.ContainsKey(userId), Is.False);
    }

    [Test]
    public async Task Test_ProcessPendingPlayTimeUpdatesAsync_WhenNoRoomsHavePendingUpdates_ShouldNotWriteToDb()
    {
        // Arrange
        var room = BuildRoomSubstitute("testroom", new Dictionary<string, TimeSpan>());
        _roomsManager.Rooms.Returns([room]);

        // Act
        await _sut.ProcessPendingPlayTimeUpdatesAsync();

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        Assert.That(await assertContext.RoomUsers.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Test_WaitForPlayTimeUpdatesAsync_ShouldCompleteImmediately_WhenNoUpdateIsRunning()
    {
        // Act => should not block
        await _sut.WaitForPlayTimeUpdatesAsync().WaitAsync(TimeSpan.FromSeconds(1));
        
        // Assert
        Assert.Pass();
    }

    private static IRoom BuildRoomSubstitute(string roomId, IDictionary<string, TimeSpan> pendingUpdates)
    {
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(roomId);
        room.PendingPlayTimeUpdates.Returns(pendingUpdates);
        return room;
    }
}
