using ElsaMina.Commands.Users.Colors;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Users.Colors;

public class NameColorsServiceTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private NameColorsService _service;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _service = new NameColorsService(_dbContextFactory);
    }

    [Test]
    public void Test_GetColor_ShouldReturnNull_WhenCacheIsEmpty()
    {
        var result = _service.GetColor("user1");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_LoadAsync_ShouldPopulateCache_WhenEntriesExistInDatabase()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.NameColors.AddRangeAsync(
                new NameColor { UserId = "user1", Color = "#ff0000" },
                new NameColor { UserId = "user2", Color = "#00ff00" }
            );
            await setupContext.SaveChangesAsync();
        }

        await _service.LoadAsync();

        Assert.That(_service.GetColor("user1"), Is.EqualTo("#ff0000"));
        Assert.That(_service.GetColor("user2"), Is.EqualTo("#00ff00"));
    }

    [Test]
    public async Task Test_LoadAsync_ShouldLeaveEmptyCache_WhenDatabaseIsEmpty()
    {
        await _service.LoadAsync();

        Assert.That(_service.GetColor("user1"), Is.Null);
    }

    [Test]
    public async Task Test_SetColorAsync_ShouldPersistColorToDatabase_WhenUserDoesNotExist()
    {
        await _service.SetColorAsync("user1", "#abc123");

        using var assertContext = new BotDbContext(_dbOptions);
        var entry = await assertContext.NameColors.FindAsync("user1");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.Color, Is.EqualTo("#abc123"));
    }

    [Test]
    public async Task Test_SetColorAsync_ShouldUpdateColorInDatabase_WhenUserAlreadyExists()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.NameColors.AddAsync(new NameColor { UserId = "user1", Color = "#ff0000" });
            await setupContext.SaveChangesAsync();
        }

        await _service.SetColorAsync("user1", "#00ff00");

        using var assertContext = new BotDbContext(_dbOptions);
        var entry = await assertContext.NameColors.FindAsync("user1");
        Assert.That(entry.Color, Is.EqualTo("#00ff00"));
    }

    [Test]
    public async Task Test_SetColorAsync_ShouldUpdateCache_WhenCalled()
    {
        await _service.SetColorAsync("user1", "#abc123");

        Assert.That(_service.GetColor("user1"), Is.EqualTo("#abc123"));
    }

    [Test]
    public async Task Test_SetColorAsync_ShouldOverwriteExistingCacheEntry_WhenUserAlreadyInCache()
    {
        await _service.SetColorAsync("user1", "#ff0000");
        await _service.SetColorAsync("user1", "#00ff00");

        Assert.That(_service.GetColor("user1"), Is.EqualTo("#00ff00"));
    }

    [Test]
    public async Task Test_DeleteColorAsync_ShouldReturnFalse_WhenUserDoesNotExist()
    {
        var result = await _service.DeleteColorAsync("nonexistent");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Test_DeleteColorAsync_ShouldReturnTrue_WhenUserExists()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.NameColors.AddAsync(new NameColor { UserId = "user1", Color = "#ff0000" });
            await setupContext.SaveChangesAsync();
        }

        var result = await _service.DeleteColorAsync("user1");

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Test_DeleteColorAsync_ShouldRemoveEntryFromDatabase_WhenUserExists()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.NameColors.AddAsync(new NameColor { UserId = "user1", Color = "#ff0000" });
            await setupContext.SaveChangesAsync();
        }

        await _service.DeleteColorAsync("user1");

        using var assertContext = new BotDbContext(_dbOptions);
        var entry = await assertContext.NameColors.FindAsync("user1");
        Assert.That(entry, Is.Null);
    }

    [Test]
    public async Task Test_DeleteColorAsync_ShouldRemoveEntryFromCache_WhenUserExists()
    {
        await _service.SetColorAsync("user1", "#ff0000");

        await _service.DeleteColorAsync("user1");

        Assert.That(_service.GetColor("user1"), Is.Null);
    }

    [Test]
    public async Task Test_DeleteColorAsync_ShouldNotAffectOtherEntries_WhenDeletingOneUser()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.NameColors.AddRangeAsync(
                new NameColor { UserId = "user1", Color = "#ff0000" },
                new NameColor { UserId = "user2", Color = "#00ff00" }
            );
            await setupContext.SaveChangesAsync();
        }

        await _service.LoadAsync();
        await _service.DeleteColorAsync("user1");

        Assert.That(_service.GetColor("user2"), Is.EqualTo("#00ff00"));
        using var assertContext = new BotDbContext(_dbOptions);
        Assert.That(await assertContext.NameColors.FindAsync("user2"), Is.Not.Null);
    }

    [Test]
    public async Task Test_LoadAsync_ShouldOverwriteExistingCacheEntries_WhenCalledAfterManualSet()
    {
        await _service.SetColorAsync("user1", "#ff0000");

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            var entry = await setupContext.NameColors.FindAsync("user1");
            entry.Color = "#123456";
            await setupContext.SaveChangesAsync();
        }

        await _service.LoadAsync();

        Assert.That(_service.GetColor("user1"), Is.EqualTo("#123456"));
    }
}
