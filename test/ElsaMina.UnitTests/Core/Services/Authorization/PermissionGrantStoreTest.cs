using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Clock;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Authorization;

public class PermissionGrantStoreTest
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private IPermissionGrantRepository _repository;
    private IClockService _clockService;
    private PermissionGrantStore _store;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IPermissionGrantRepository>();
        _repository.LoadAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<PermissionGrant>());
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(NOW);
        _store = new PermissionGrantStore(_repository, _clockService);
    }

    [Test]
    public async Task Test_OnStartingAsync_ShouldDeleteExpiredGrantsThenLoadActiveOnes()
    {
        // Arrange
        var active = CreateGrant("room", "alice", PermissionGrantKind.Rank, "%");
        var expired = CreateGrant("room", "bob", PermissionGrantKind.Rank, "%", NOW.AddMinutes(-1));
        _repository.LoadAllAsync(Arg.Any<CancellationToken>()).Returns([active, expired]);

        // Act
        await _store.OnStartingAsync(CancellationToken.None);

        // Assert
        Received.InOrder(() =>
        {
            _repository.DeleteExpiredAsync(NOW, Arg.Any<CancellationToken>());
            _repository.LoadAllAsync(Arg.Any<CancellationToken>());
        });
        Assert.That(_store.GetActiveGrants("room"), Is.EqualTo(new[] { active }));
    }

    [Test]
    public async Task Test_GetActiveGrants_ShouldNotReturnGrant_OnceItHasExpired()
    {
        // Arrange
        var grant = CreateGrant("room", "alice", PermissionGrantKind.Rank, "%", NOW.AddHours(1));
        await _store.SaveAsync(grant);

        // Act
        var beforeExpiry = _store.GetActiveGrants("room", "alice");
        _clockService.CurrentUtcDateTimeOffset.Returns(NOW.AddHours(1));
        var afterExpiry = _store.GetActiveGrants("room", "alice");

        // Assert
        Assert.That(beforeExpiry, Is.EqualTo(new[] { grant }));
        Assert.That(afterExpiry, Is.Empty);
    }

    [Test]
    public async Task Test_GetActiveGrants_ShouldOnlyReturnGrantsOfTheRoomAndUser()
    {
        // Arrange
        var aliceGrant = CreateGrant("room", "alice", PermissionGrantKind.Command, "add-badge");
        var bobGrant = CreateGrant("room", "bob", PermissionGrantKind.Command, "add-badge");
        var otherRoomGrant = CreateGrant("otherroom", "alice", PermissionGrantKind.Command, "add-badge");
        await _store.SaveAsync(aliceGrant);
        await _store.SaveAsync(bobGrant);
        await _store.SaveAsync(otherRoomGrant);

        // Act & Assert
        Assert.That(_store.GetActiveGrants("room", "alice"), Is.EqualTo(new[] { aliceGrant }));
        Assert.That(_store.GetActiveGrants("room"), Is.EquivalentTo(new[] { aliceGrant, bobGrant }));
        Assert.That(_store.GetActiveGrants("nowhere", "alice"), Is.Empty);
    }

    [Test]
    public async Task Test_SaveAsync_ShouldReplaceGrant_WhenUserAlreadyHasTheSameTarget()
    {
        // Arrange
        var first = CreateGrant("room", "alice", PermissionGrantKind.Command, "add-badge", NOW.AddHours(1));
        var second = CreateGrant("room", "alice", PermissionGrantKind.Command, "add-badge", NOW.AddDays(1));
        await _store.SaveAsync(first);

        // Act
        await _store.SaveAsync(second);

        // Assert
        Assert.That(_store.GetActiveGrants("room", "alice"), Is.EqualTo(new[] { second }));
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<PermissionGrant>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveAsync(second, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_SaveAsync_ShouldDeletePreviousRankGrant_WhenRankChanges()
    {
        // Arrange
        var voice = CreateGrant("room", "alice", PermissionGrantKind.Rank, "+");
        var driver = CreateGrant("room", "alice", PermissionGrantKind.Rank, "%");
        await _store.SaveAsync(voice);

        // Act
        await _store.SaveAsync(driver);

        // Assert
        Assert.That(_store.GetActiveGrants("room", "alice"), Is.EqualTo(new[] { driver }));
        await _repository.Received(1).DeleteAsync(voice, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_SaveAsync_ShouldKeepOtherGrantsOfTheUser()
    {
        // Arrange
        var rank = CreateGrant("room", "alice", PermissionGrantKind.Rank, "+");
        var command = CreateGrant("room", "alice", PermissionGrantKind.Command, "add-badge");

        // Act
        await _store.SaveAsync(rank);
        await _store.SaveAsync(command);

        // Assert
        Assert.That(_store.GetActiveGrants("room", "alice"), Is.EquivalentTo(new[] { rank, command }));
    }

    [Test]
    public void Test_SaveAsync_ShouldNotKeepGrantInMemory_WhenRepositoryFails()
    {
        // Arrange
        var grant = CreateGrant("room", "alice", PermissionGrantKind.Rank, "%");
        _repository.SaveAsync(grant, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("db down")));

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _store.SaveAsync(grant));
        Assert.That(_store.GetActiveGrants("room", "alice"), Is.Empty);
    }

    [Test]
    public async Task Test_RemoveAsync_ShouldRemoveGrant_WhenUserHasIt()
    {
        // Arrange
        var grant = CreateGrant("room", "alice", PermissionGrantKind.Rank, "%");
        await _store.SaveAsync(grant);

        // Act
        var removed = await _store.RemoveAsync("room", "alice", PermissionGrantKind.Rank, "+");

        // Assert
        Assert.That(removed, Is.True);
        Assert.That(_store.GetActiveGrants("room", "alice"), Is.Empty);
        await _repository.Received(1).DeleteAsync(grant, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RemoveAsync_ShouldReturnFalse_WhenUserDoesNotHaveTheGrant()
    {
        // Arrange
        await _store.SaveAsync(CreateGrant("room", "alice", PermissionGrantKind.Command, "add-badge"));

        // Act
        var removed = await _store.RemoveAsync("room", "alice", PermissionGrantKind.Command, "givebadge");

        // Assert
        Assert.That(removed, Is.False);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<PermissionGrant>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RemoveAsync_ShouldDeleteButReturnFalse_WhenGrantHasAlreadyExpired()
    {
        // Arrange
        var grant = CreateGrant("room", "alice", PermissionGrantKind.Rank, "%", NOW.AddMinutes(5));
        await _store.SaveAsync(grant);
        _clockService.CurrentUtcDateTimeOffset.Returns(NOW.AddMinutes(10));

        // Act
        var removed = await _store.RemoveAsync("room", "alice", PermissionGrantKind.Rank, "%");

        // Assert
        Assert.That(removed, Is.False);
        await _repository.Received(1).DeleteAsync(grant, Arg.Any<CancellationToken>());
    }

    private static PermissionGrant CreateGrant(string roomId, string userId, PermissionGrantKind kind,
        string target, DateTimeOffset? expiresAt = null) =>
        new(roomId, userId, kind, target, "owner", NOW, expiresAt);
}
