using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ElsaMina.UnitTests.DataAccess;

public class EfRoomParameterRepositoryTest
{
    private DbContextOptions<BotDbContext> _options;
    private EfRoomParameterRepository _repository;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _repository = new EfRoomParameterRepository(
            new BotDbContextFactory(new PooledDbContextFactory<BotDbContext>(_options)));
    }

    [Test]
    public async Task Test_LoadOrCreateRoomAsync_ShouldCreateRoom_WhenItDoesNotExist()
    {
        // Act
        var values = await _repository.LoadOrCreateRoomAsync("room", "Room");

        // Assert
        Assert.That(values, Is.Empty);
        await using var dbContext = new BotDbContext(_options);
        var savedRoom = await dbContext.RoomInfo.FindAsync("room");
        Assert.That(savedRoom?.Title, Is.EqualTo("Room"));
    }

    [Test]
    public async Task Test_TrySaveParameterValueAsync_ShouldInsertThenUpdateValue()
    {
        // Arrange
        await _repository.LoadOrCreateRoomAsync("room", "Room");

        // Act
        var inserted = await _repository.TrySaveParameterValueAsync("room", "bck", "True");
        var updated = await _repository.TrySaveParameterValueAsync("room", "bck", "False");
        var values = await _repository.LoadOrCreateRoomAsync("room", "Renamed room");

        // Assert
        Assert.That(inserted, Is.True);
        Assert.That(updated, Is.True);
        Assert.That(values, Is.EqualTo(new Dictionary<string, string> { ["bck"] = "False" }));
        await using var dbContext = new BotDbContext(_options);
        Assert.That((await dbContext.RoomInfo.FindAsync("room"))?.Title, Is.EqualTo("Renamed room"));
    }
}
