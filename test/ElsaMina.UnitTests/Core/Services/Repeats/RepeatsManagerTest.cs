using ElsaMina.Core;
using ElsaMina.Core.Services.Repeats;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Repeats;

public class RepeatsManagerTest
{
    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _factory;
    private IBot _bot;
    private RepeatsManager _manager;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // isolate each test
            .Options;

        _factory = Substitute.For<IBotDbContextFactory>();
        _factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));

        _bot = Substitute.For<IBot>();

        _manager = new RepeatsManager(_factory, new Lazy<IBot>(() => _bot));
    }

    private async Task<int> CountRepeatsInDatabaseAsync()
    {
        await using var dbContext = new BotDbContext(_options);
        return await dbContext.Repeats.CountAsync();
    }

    [Test]
    public async Task Test_StartRepeatAsync_ShouldPersistRepeatAndTrackItInMemory_WhenCalled()
    {
        // Act
        await _manager.StartRepeatAsync("testroom", "hello", TimeSpan.FromHours(1));

        // Assert
        var trackedRepeats = _manager.GetRepeats("testroom").ToList();
        Assert.That(trackedRepeats, Has.Count.EqualTo(1));
        Assert.That(trackedRepeats[0].Message, Is.EqualTo("hello"));
        Assert.That(await CountRepeatsInDatabaseAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Test_StopRepeatAsync_ShouldRemoveRepeatFromMemoryAndDatabase_WhenRepeatExists()
    {
        // Arrange
        await _manager.StartRepeatAsync("testroom", "hello", TimeSpan.FromHours(1));
        var repeatId = _manager.GetRepeats("testroom").Single().RepeatId;

        // Act
        var stopped = await _manager.StopRepeatAsync(repeatId);

        // Assert
        Assert.That(stopped, Is.True);
        Assert.That(_manager.GetRepeat(repeatId), Is.Null);
        Assert.That(await CountRepeatsInDatabaseAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task Test_StopRepeatAsync_ShouldReturnFalse_WhenRepeatDoesNotExist()
    {
        // Act
        var stopped = await _manager.StopRepeatAsync(Guid.NewGuid());

        // Assert
        Assert.That(stopped, Is.False);
    }

    [Test]
    public async Task Test_InitializeAsync_ShouldReloadPersistedRepeatsAndStartThem_WhenCalled()
    {
        // Arrange
        var repeatId = Guid.NewGuid();
        await using (var dbContext = new BotDbContext(_options))
        {
            dbContext.Repeats.Add(new SavedRepeat
            {
                Id = repeatId,
                RoomId = "testroom",
                Message = "persisted",
                Interval = TimeSpan.FromHours(1)
            });
            await dbContext.SaveChangesAsync();
        }

        // Act
        await _manager.InitializeAsync();

        // Assert
        var reloaded = _manager.GetRepeat(repeatId);
        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded.RoomId, Is.EqualTo("testroom"));
        Assert.That(reloaded.Message, Is.EqualTo("persisted"));
    }
}
