using ElsaMina.Commands.Alerts;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Alerts;

public class AlertsManagerTest
{
    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _dbContextFactory;
    private AlertsManager _alertsManager;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));

        _alertsManager = new AlertsManager(_dbContextFactory);
    }

    private async Task<List<ChannelAlert>> ReadAllFromDbAsync()
    {
        await using var dbContext = new BotDbContext(_options);
        return await dbContext.ChannelAlerts.ToListAsync();
    }

    [Test]
    public async Task Test_InitializeAsync_ShouldLoadStoredAlerts_FromDb()
    {
        // Arrange
        await using (var dbContext = new BotDbContext(_options))
        {
            dbContext.ChannelAlerts.Add(new ChannelAlert
                { RoomId = "room", Platform = "twitch", ChannelId = "1", ChannelName = "streamer" });
            await dbContext.SaveChangesAsync();
        }

        // Act
        await _alertsManager.InitializeAsync();

        // Assert
        Assert.That(_alertsManager.GetRoomAlerts("room"),
            Is.EquivalentTo(new[] { new AlertSubscription("room", "twitch", "1", "streamer") }));
    }

    [Test]
    public async Task Test_AddAlertAsync_ShouldPersistAlert_WhenAlertIsNew()
    {
        // Act
        var isAdded = await _alertsManager.AddAlertAsync("room", "twitch", new AlertChannel("1", "streamer"));

        // Assert
        Assert.That(isAdded, Is.True);
        var storedAlerts = await ReadAllFromDbAsync();
        Assert.That(storedAlerts, Has.Count.EqualTo(1));
        Assert.That(storedAlerts[0].ChannelName, Is.EqualTo("streamer"));
    }

    [Test]
    public async Task Test_AddAlertAsync_ShouldReturnFalse_WhenAlertAlreadyExists()
    {
        // Arrange
        await _alertsManager.AddAlertAsync("room", "twitch", new AlertChannel("1", "streamer"));

        // Act
        var isAdded = await _alertsManager.AddAlertAsync("room", "twitch", new AlertChannel("1", "streamer"));

        // Assert
        Assert.That(isAdded, Is.False);
        Assert.That(await ReadAllFromDbAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Test_GetAlerts_ShouldFilterByPlatform()
    {
        // Arrange
        await _alertsManager.AddAlertAsync("room", "twitch", new AlertChannel("1", "streamer"));
        await _alertsManager.AddAlertAsync("room", "youtube", new AlertChannel("UC1", "youtuber"));
        await _alertsManager.AddAlertAsync("room", "youtubelive", new AlertChannel("UC1", "youtuber"));

        // Act
        var youtubeAlerts = _alertsManager.GetAlerts("youtube", "youtubelive");

        // Assert
        Assert.That(youtubeAlerts.Select(alert => alert.Platform),
            Is.EquivalentTo(new[] { "youtube", "youtubelive" }));
    }

    [TestCase("streamer")]
    [TestCase("@Streamer")]
    [TestCase("1")]
    public async Task Test_RemoveAlertAsync_ShouldRemoveAlert_WhenMatchingByNameOrId(string channel)
    {
        // Arrange
        await _alertsManager.AddAlertAsync("room", "twitch", new AlertChannel("1", "streamer"));

        // Act
        var removedAlert = await _alertsManager.RemoveAlertAsync("room", "twitch", channel);

        // Assert
        Assert.That(removedAlert, Is.EqualTo(new AlertSubscription("room", "twitch", "1", "streamer")));
        Assert.That(_alertsManager.GetRoomAlerts("room"), Is.Empty);
        Assert.That(await ReadAllFromDbAsync(), Is.Empty);
    }

    [Test]
    public async Task Test_RemoveAlertAsync_ShouldReturnNull_WhenAlertDoesNotExist()
    {
        // Arrange
        await _alertsManager.AddAlertAsync("room", "twitch", new AlertChannel("1", "streamer"));

        // Act
        var removedAlert = await _alertsManager.RemoveAlertAsync("otherroom", "twitch", "streamer");

        // Assert
        Assert.That(removedAlert, Is.Null);
        Assert.That(await ReadAllFromDbAsync(), Has.Count.EqualTo(1));
    }
}
