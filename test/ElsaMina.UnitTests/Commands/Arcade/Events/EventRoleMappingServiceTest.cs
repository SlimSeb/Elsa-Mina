using ElsaMina.Commands.Arcade.Events;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Events;

public class EventRoleMappingServiceTest
{
    private BotDbContext _db;
    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _dbContextFactory;
    private EventRoleMappingService _service;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new BotDbContext(_options);

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));

        _service = new EventRoleMappingService(_dbContextFactory);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public async Task Test_GetMappingsForRoom_ShouldReturnOnlyThatRoomOrderedByEventName()
    {
        // Arrange
        await _db.EventRoleMappings.AddRangeAsync(
            new EventRoleMapping { EventName = "zelda", RoomId = "room1", DiscordRoleId = "1" },
            new EventRoleMapping { EventName = "arceus", RoomId = "room1", DiscordRoleId = "2" },
            new EventRoleMapping { EventName = "other", RoomId = "room2", DiscordRoleId = "3" });
        await _db.SaveChangesAsync();

        // Act
        var mappings = await _service.GetMappingsForRoomAsync("room1");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(mappings, Has.Count.EqualTo(2));
            Assert.That(mappings[0].EventName, Is.EqualTo("arceus"));
            Assert.That(mappings[1].EventName, Is.EqualTo("zelda"));
        }
    }

    [Test]
    public async Task Test_GetMapping_ShouldReturnMatchingMapping()
    {
        // Arrange
        await _db.EventRoleMappings.AddAsync(
            new EventRoleMapping { EventName = "zelda", RoomId = "room1", DiscordRoleId = "42" });
        await _db.SaveChangesAsync();

        // Act
        var mapping = await _service.GetMappingAsync("zelda", "room1");

        // Assert
        Assert.That(mapping, Is.Not.Null);
        Assert.That(mapping.DiscordRoleId, Is.EqualTo("42"));
    }

    [Test]
    public async Task Test_GetMapping_ShouldReturnNull_WhenNotFound()
    {
        var mapping = await _service.GetMappingAsync("missing", "room1");

        Assert.That(mapping, Is.Null);
    }

    [Test]
    public async Task Test_SaveMapping_ShouldInsertNewMapping_WhenNotExisting()
    {
        // Act
        await _service.SaveMappingAsync(new EventRoleMapping
        {
            EventName = "zelda", RoomId = "room1", DiscordRoleId = "42"
        });

        // Assert
        await using var context = new BotDbContext(_options);
        var stored = await context.EventRoleMappings.FindAsync("zelda", "room1");
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored.DiscordRoleId, Is.EqualTo("42"));
    }

    [Test]
    public async Task Test_SaveMapping_ShouldUpdateRoleId_WhenMappingExists()
    {
        // Arrange
        await _db.EventRoleMappings.AddAsync(
            new EventRoleMapping { EventName = "zelda", RoomId = "room1", DiscordRoleId = "old" });
        await _db.SaveChangesAsync();

        // Act
        await _service.SaveMappingAsync(new EventRoleMapping
        {
            EventName = "zelda", RoomId = "room1", DiscordRoleId = "new"
        });

        // Assert
        await using var context = new BotDbContext(_options);
        var stored = await context.EventRoleMappings.FindAsync("zelda", "room1");
        Assert.That(stored.DiscordRoleId, Is.EqualTo("new"));
        Assert.That(await context.EventRoleMappings.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Test_DeleteMapping_ShouldRemoveMapping_WhenExisting()
    {
        // Arrange
        await _db.EventRoleMappings.AddAsync(
            new EventRoleMapping { EventName = "zelda", RoomId = "room1", DiscordRoleId = "42" });
        await _db.SaveChangesAsync();

        // Act
        await _service.DeleteMappingAsync("zelda", "room1");

        // Assert
        await using var context = new BotDbContext(_options);
        Assert.That(await context.EventRoleMappings.FindAsync("zelda", "room1"), Is.Null);
    }

    [Test]
    public void Test_DeleteMapping_ShouldNotThrow_WhenMappingDoesNotExist()
    {
        Assert.DoesNotThrowAsync(() => _service.DeleteMappingAsync("missing", "room1"));
    }
}
