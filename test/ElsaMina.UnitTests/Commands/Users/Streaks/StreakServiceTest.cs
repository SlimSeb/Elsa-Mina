using ElsaMina.Commands.Users.Streaks;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Users.Streaks;

public class StreakServiceTest
{
    private static DbContextOptions<BotDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static IBotDbContextFactory CreateFactory(DbContextOptions<BotDbContext> options)
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(new BotDbContext(options));
        return factory;
    }

    private static async Task SeedRoomUserAsync(DbContextOptions<BotDbContext> options, RoomUser roomUser)
    {
        await using var db = new BotDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.RoomUsers.Add(roomUser);
        await db.SaveChangesAsync();
    }

    [Test]
    public async Task Test_UpdateStreakAsync_ShouldDoNothing_WhenRoomUserDoesNotExist()
    {
        // Arrange
        var options = CreateOptions();
        await using var db = new BotDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new StreakService(CreateFactory(options));

        // Act
        await service.UpdateStreakAsync("unknownuser", "testroom", DateOnly.FromDateTime(DateTime.Today));

        // Assert - no exception and DB still empty
        Assert.That(await db.RoomUsers.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task Test_UpdateStreakAsync_ShouldStartStreakAtOne_WhenFirstActivity()
    {
        // Arrange
        var options = CreateOptions();
        var today = new DateOnly(2026, 5, 13);
        await SeedRoomUserAsync(options, new RoomUser { Id = "alice", RoomId = "testroom" });
        var service = new StreakService(CreateFactory(options));

        // Act
        await service.UpdateStreakAsync("alice", "testroom", today);

        // Assert
        await using var db = new BotDbContext(options);
        var roomUser = await db.RoomUsers.FindAsync("alice", "testroom");
        Assert.That(roomUser.CurrentStreak, Is.EqualTo(1));
        Assert.That(roomUser.LongestStreak, Is.EqualTo(1));
        Assert.That(roomUser.LastActivityDate, Is.EqualTo(today));
    }

    [Test]
    public async Task Test_UpdateStreakAsync_ShouldIncrementStreak_WhenActivityIsOnConsecutiveDay()
    {
        // Arrange
        var options = CreateOptions();
        var yesterday = new DateOnly(2026, 5, 12);
        var today = new DateOnly(2026, 5, 13);
        await SeedRoomUserAsync(options, new RoomUser
        {
            Id = "alice", RoomId = "testroom",
            CurrentStreak = 3, LongestStreak = 5, LastActivityDate = yesterday
        });
        var service = new StreakService(CreateFactory(options));

        // Act
        await service.UpdateStreakAsync("alice", "testroom", today);

        // Assert
        await using var db = new BotDbContext(options);
        var roomUser = await db.RoomUsers.FindAsync("alice", "testroom");
        Assert.That(roomUser.CurrentStreak, Is.EqualTo(4));
        Assert.That(roomUser.LongestStreak, Is.EqualTo(5));
        Assert.That(roomUser.LastActivityDate, Is.EqualTo(today));
    }

    [Test]
    public async Task Test_UpdateStreakAsync_ShouldUpdateLongestStreak_WhenCurrentExceedsPrevious()
    {
        // Arrange
        var options = CreateOptions();
        var yesterday = new DateOnly(2026, 5, 12);
        var today = new DateOnly(2026, 5, 13);
        await SeedRoomUserAsync(options, new RoomUser
        {
            Id = "alice", RoomId = "testroom",
            CurrentStreak = 5, LongestStreak = 5, LastActivityDate = yesterday
        });
        var service = new StreakService(CreateFactory(options));

        // Act
        await service.UpdateStreakAsync("alice", "testroom", today);

        // Assert
        await using var db = new BotDbContext(options);
        var roomUser = await db.RoomUsers.FindAsync("alice", "testroom");
        Assert.That(roomUser.CurrentStreak, Is.EqualTo(6));
        Assert.That(roomUser.LongestStreak, Is.EqualTo(6));
    }

    [Test]
    public async Task Test_UpdateStreakAsync_ShouldResetStreak_WhenGapIsMoreThanOneDay()
    {
        // Arrange
        var options = CreateOptions();
        var twoDaysAgo = new DateOnly(2026, 5, 11);
        var today = new DateOnly(2026, 5, 13);
        await SeedRoomUserAsync(options, new RoomUser
        {
            Id = "alice", RoomId = "testroom",
            CurrentStreak = 10, LongestStreak = 10, LastActivityDate = twoDaysAgo
        });
        var service = new StreakService(CreateFactory(options));

        // Act
        await service.UpdateStreakAsync("alice", "testroom", today);

        // Assert
        await using var db = new BotDbContext(options);
        var roomUser = await db.RoomUsers.FindAsync("alice", "testroom");
        Assert.That(roomUser.CurrentStreak, Is.EqualTo(1));
        Assert.That(roomUser.LongestStreak, Is.EqualTo(10));
        Assert.That(roomUser.LastActivityDate, Is.EqualTo(today));
    }

    [Test]
    public async Task Test_UpdateStreakAsync_ShouldSkipWrite_WhenAlreadyUpdatedToday()
    {
        // Arrange
        var options = CreateOptions();
        var today = new DateOnly(2026, 5, 13);
        await SeedRoomUserAsync(options, new RoomUser
        {
            Id = "alice", RoomId = "testroom",
            CurrentStreak = 1, LongestStreak = 1, LastActivityDate = today
        });
        var factory = Substitute.For<IBotDbContextFactory>();
        var callCount = 0;
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return new BotDbContext(options);
            });
        var service = new StreakService(factory);

        // First call populates in-memory cache
        await service.UpdateStreakAsync("alice", "testroom", today);
        callCount = 0;

        // Act - second call same day
        await service.UpdateStreakAsync("alice", "testroom", today);

        // Assert - no DB access on second call
        Assert.That(callCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_GetStreakAsync_ShouldReturnZeroes_WhenRoomUserDoesNotExist()
    {
        // Arrange
        var options = CreateOptions();
        await using var db = new BotDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new StreakService(CreateFactory(options));

        // Act
        var (current, longest) = await service.GetStreakAsync("nobody", "testroom");

        // Assert
        Assert.That(current, Is.EqualTo(0));
        Assert.That(longest, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_GetStreakAsync_ShouldReturnStoredValues_WhenRoomUserExists()
    {
        // Arrange
        var options = CreateOptions();
        await SeedRoomUserAsync(options, new RoomUser
        {
            Id = "alice", RoomId = "testroom",
            CurrentStreak = 7, LongestStreak = 12
        });
        var service = new StreakService(CreateFactory(options));

        // Act
        var (current, longest) = await service.GetStreakAsync("alice", "testroom");

        // Assert
        Assert.That(current, Is.EqualTo(7));
        Assert.That(longest, Is.EqualTo(12));
    }
}
