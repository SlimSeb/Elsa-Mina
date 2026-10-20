using ElsaMina.Core.Services.Authorization;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ElsaMina.UnitTests.DataAccess;

public class EfPermissionGrantRepositoryTest
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private DbContextOptions<BotDbContext> _options;
    private EfPermissionGrantRepository _repository;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _repository = new EfPermissionGrantRepository(
            new BotDbContextFactory(new PooledDbContextFactory<BotDbContext>(_options)));
    }

    [Test]
    public async Task Test_SaveAsync_ShouldInsertThenUpdateGrant_WhenKeyIsTheSame()
    {
        // Arrange
        var grant = CreateGrant(PermissionGrantKind.Command, "add-badge", NOW.AddDays(1));

        // Act
        await _repository.SaveAsync(grant);
        await _repository.SaveAsync(grant with { GrantedBy = "otherowner", ExpiresAt = null });
        var grants = await _repository.LoadAllAsync();

        // Assert
        Assert.That(grants, Is.EqualTo(new[] { grant with { GrantedBy = "otherowner", ExpiresAt = null } }));
    }

    [Test]
    public async Task Test_DeleteAsync_ShouldRemoveOnlyThatGrant()
    {
        // Arrange
        var rank = CreateGrant(PermissionGrantKind.Rank, "%");
        var command = CreateGrant(PermissionGrantKind.Command, "add-badge");
        await _repository.SaveAsync(rank);
        await _repository.SaveAsync(command);

        // Act
        await _repository.DeleteAsync(rank);
        await _repository.DeleteAsync(CreateGrant(PermissionGrantKind.Category, "Badges"));

        // Assert
        Assert.That(await _repository.LoadAllAsync(), Is.EqualTo(new[] { command }));
    }

    [Test]
    public async Task Test_DeleteExpiredAsync_ShouldKeepPermanentAndActiveGrants()
    {
        // Arrange
        var permanent = CreateGrant(PermissionGrantKind.Rank, "%");
        var active = CreateGrant(PermissionGrantKind.Command, "add-badge", NOW.AddMinutes(1));
        var expired = CreateGrant(PermissionGrantKind.Command, "givebadge", NOW);
        await _repository.SaveAsync(permanent);
        await _repository.SaveAsync(active);
        await _repository.SaveAsync(expired);

        // Act
        await _repository.DeleteExpiredAsync(NOW);

        // Assert
        Assert.That(await _repository.LoadAllAsync(), Is.EquivalentTo(new[] { permanent, active }));
    }

    [Test]
    public async Task Test_LoadAllAsync_ShouldIgnoreRows_WhenKindIsUnknown()
    {
        // Arrange
        await using (var dbContext = new BotDbContext(_options))
        {
            dbContext.PermissionGrants.Add(new SavedPermissionGrant
            {
                RoomId = "room", UserId = "alice", Kind = "Removed", Target = "x", GrantedAt = NOW
            });
            await dbContext.SaveChangesAsync();
        }

        // Act
        var grants = await _repository.LoadAllAsync();

        // Assert
        Assert.That(grants, Is.Empty);
    }

    private static PermissionGrant CreateGrant(PermissionGrantKind kind, string target,
        DateTimeOffset? expiresAt = null) =>
        new("room", "alice", kind, target, "owner", NOW, expiresAt);
}
