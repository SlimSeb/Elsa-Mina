using ElsaMina.Commands.Showdown.Ladder.EloHistory;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Showdown.Ladder.EloHistory;

public class EloProgressionManagerTest
{
    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _factory;
    private EloProgressionManager _manager;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _factory = Substitute.For<IBotDbContextFactory>();
        _factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));

        _manager = new EloProgressionManager(_factory);
    }

    private async Task SeedAsync(params (string Format, string UserId)[] entries)
    {
        await using var db = new BotDbContext(_options);
        db.TrackedEloUsers.AddRange(entries.Select(e => new TrackedEloUser { Format = e.Format, UserId = e.UserId }));
        await db.SaveChangesAsync();
    }

    private async Task<List<TrackedEloUser>> ReadAllFromDbAsync()
    {
        await using var db = new BotDbContext(_options);
        return await db.TrackedEloUsers.ToListAsync();
    }

    // InitializeAsync

    [Test]
    public async Task Test_InitializeAsync_ShouldLoadAllStoredUsers_FromDb()
    {
        // Arrange
        await SeedAsync(("gen9ou", "alice"), ("gen8ou", "bob"));

        // Act
        await _manager.InitializeAsync();

        // Assert
        Assert.That(_manager.GetAllTrackedUsers(), Is.EquivalentTo(new[]
        {
            new EloTrackedUser("gen9ou", "alice"),
            new EloTrackedUser("gen8ou", "bob"),
        }));
    }

    [Test]
    public async Task Test_InitializeAsync_ShouldLeaveSetEmpty_WhenDbIsEmpty()
    {
        // Act
        await _manager.InitializeAsync();

        // Assert
        Assert.That(_manager.GetAllTrackedUsers(), Is.Empty);
    }

    // GetAllTrackedUsers

    [Test]
    public void Test_GetAllTrackedUsers_ShouldReturnEmpty_WhenNothingTracked()
    {
        Assert.That(_manager.GetAllTrackedUsers(), Is.Empty);
    }

    // TrackUserAsync

    [Test]
    public async Task Test_TrackUserAsync_ShouldReturnTrue_WhenUserIsNew()
    {
        var result = await _manager.TrackUserAsync("gen9ou", "alice");

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Test_TrackUserAsync_ShouldAddUserToInMemorySet()
    {
        await _manager.TrackUserAsync("gen9ou", "alice");

        Assert.That(_manager.GetAllTrackedUsers(),
            Is.EquivalentTo(new[] { new EloTrackedUser("gen9ou", "alice") }));
    }

    [Test]
    public async Task Test_TrackUserAsync_ShouldPersistUserToDb()
    {
        await _manager.TrackUserAsync("gen9ou", "alice");

        var stored = await ReadAllFromDbAsync();
        Assert.That(stored, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(stored[0].Format, Is.EqualTo("gen9ou"));
            Assert.That(stored[0].UserId, Is.EqualTo("alice"));
        });
    }

    [Test]
    public async Task Test_TrackUserAsync_ShouldReturnFalse_WhenUserAlreadyTracked()
    {
        await _manager.TrackUserAsync("gen9ou", "alice");

        var result = await _manager.TrackUserAsync("gen9ou", "alice");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Test_TrackUserAsync_ShouldNotDuplicateInDb_WhenUserAlreadyTracked()
    {
        await _manager.TrackUserAsync("gen9ou", "alice");
        await _manager.TrackUserAsync("gen9ou", "alice");

        var stored = await ReadAllFromDbAsync();
        Assert.That(stored, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Test_TrackUserAsync_ShouldTreatSameUserInDifferentFormatsAsDistinct()
    {
        var first = await _manager.TrackUserAsync("gen9ou", "alice");
        var second = await _manager.TrackUserAsync("gen8ou", "alice");

        Assert.Multiple(async () =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(_manager.GetAllTrackedUsers(), Has.Count.EqualTo(2));
            Assert.That(await ReadAllFromDbAsync(), Has.Count.EqualTo(2));
        });
    }

    // UntrackUserAsync

    [Test]
    public async Task Test_UntrackUserAsync_ShouldReturnTrue_WhenUserWasTracked()
    {
        await _manager.TrackUserAsync("gen9ou", "alice");

        var result = await _manager.UntrackUserAsync("gen9ou", "alice");

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Test_UntrackUserAsync_ShouldRemoveUserFromInMemorySet()
    {
        await _manager.TrackUserAsync("gen9ou", "alice");
        await _manager.UntrackUserAsync("gen9ou", "alice");

        Assert.That(_manager.GetAllTrackedUsers(), Is.Empty);
    }

    [Test]
    public async Task Test_UntrackUserAsync_ShouldDeleteUserFromDb()
    {
        await _manager.TrackUserAsync("gen9ou", "alice");
        await _manager.UntrackUserAsync("gen9ou", "alice");

        Assert.That(await ReadAllFromDbAsync(), Is.Empty);
    }

    [Test]
    public async Task Test_UntrackUserAsync_ShouldReturnFalse_WhenUserWasNotTracked()
    {
        var result = await _manager.UntrackUserAsync("gen9ou", "alice");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Test_UntrackUserAsync_ShouldNotTouchDb_WhenUserWasNotTracked()
    {
        await _manager.UntrackUserAsync("gen9ou", "alice");

        await _factory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_UntrackUserAsync_ShouldOnlyRemoveMatchingEntry()
    {
        await _manager.TrackUserAsync("gen9ou", "alice");
        await _manager.TrackUserAsync("gen8ou", "alice");

        await _manager.UntrackUserAsync("gen9ou", "alice");

        Assert.Multiple(async () =>
        {
            Assert.That(_manager.GetAllTrackedUsers(),
                Is.EquivalentTo(new[] { new EloTrackedUser("gen8ou", "alice") }));
            var stored = await ReadAllFromDbAsync();
            Assert.That(stored, Has.Count.EqualTo(1));
            Assert.That(stored[0].Format, Is.EqualTo("gen8ou"));
        });
    }

    // GetAllTrackedUsers with multiple entries

    [Test]
    public async Task Test_GetAllTrackedUsers_ShouldReturnAllTrackedUsers()
    {
        await _manager.TrackUserAsync("gen9ou", "alice");
        await _manager.TrackUserAsync("gen9ou", "bob");
        await _manager.TrackUserAsync("gen8ou", "alice");

        var result = _manager.GetAllTrackedUsers();

        Assert.That(result, Is.EquivalentTo(new[]
        {
            new EloTrackedUser("gen9ou", "alice"),
            new EloTrackedUser("gen9ou", "bob"),
            new EloTrackedUser("gen8ou", "alice"),
        }));
    }
}
