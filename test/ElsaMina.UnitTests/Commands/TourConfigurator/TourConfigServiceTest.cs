using ElsaMina.Commands.TourConfigurator;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.TourConfigurator;

[TestFixture]
public class TourConfigServiceTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private TourConfigService _sut;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _sut = new TourConfigService(_dbContextFactory);
    }

    [Test]
    public async Task Test_GetTourConfigsForRoomAsync_ShouldReturnEmpty_WhenNoConfigsExist()
    {
        var result = await _sut.GetTourConfigsForRoomAsync("room1");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_GetTourConfigsForRoomAsync_ShouldReturnOnlyConfigsForRoom_WhenMultipleRoomsExist()
    {
        using (var setupCtx = new BotDbContext(_dbOptions))
        {
            setupCtx.TourConfigs.AddRange(
                new TourConfig { Id = "tour1", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 5 },
                new TourConfig { Id = "tour2", RoomId = "room2", Tier = "UU", Format = "elim", Autostart = 5 }
            );
            await setupCtx.SaveChangesAsync();
        }

        var result = await _sut.GetTourConfigsForRoomAsync("room1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo("tour1"));
        }
    }

    [Test]
    public async Task Test_GetTourConfigsForRoomAsync_ShouldReturnConfigsOrderedById()
    {
        using (var setupCtx = new BotDbContext(_dbOptions))
        {
            setupCtx.TourConfigs.AddRange(
                new TourConfig { Id = "ztour", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 5 },
                new TourConfig { Id = "atour", RoomId = "room1", Tier = "UU", Format = "elim", Autostart = 5 }
            );
            await setupCtx.SaveChangesAsync();
        }

        var result = await _sut.GetTourConfigsForRoomAsync("room1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].Id, Is.EqualTo("atour"));
            Assert.That(result[1].Id, Is.EqualTo("ztour"));
        }
    }

    [Test]
    public async Task Test_GetTourConfigAsync_ShouldReturnNull_WhenConfigDoesNotExist()
    {
        var result = await _sut.GetTourConfigAsync("nonexistent", "room1");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetTourConfigAsync_ShouldReturnConfig_WhenConfigExists()
    {
        using (var setupCtx = new BotDbContext(_dbOptions))
        {
            setupCtx.TourConfigs.Add(new TourConfig
            {
                Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim",
                Autostart = 10, TourName = "OU Tour"
            });
            await setupCtx.SaveChangesAsync();
        }

        var result = await _sut.GetTourConfigAsync("outils", "room1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("outils"));
            Assert.That(result.TourName, Is.EqualTo("OU Tour"));
        }
    }

    [Test]
    public async Task Test_GetTourConfigAsync_ShouldReturnNull_WhenRoomIdDoesNotMatch()
    {
        using (var setupCtx = new BotDbContext(_dbOptions))
        {
            setupCtx.TourConfigs.Add(new TourConfig
            {
                Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 10
            });
            await setupCtx.SaveChangesAsync();
        }

        var result = await _sut.GetTourConfigAsync("outils", "room2");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_SaveTourConfigAsync_ShouldAddNewConfig_WhenConfigDoesNotExist()
    {
        var tourConfig = new TourConfig
        {
            Id = "newtour", RoomId = "room1", Tier = "OU", Format = "elim",
            Autostart = 10, TourName = "New Tour", Rules = "Sleep Clause"
        };

        await _sut.SaveTourConfigAsync(tourConfig);

        using var assertCtx = new BotDbContext(_dbOptions);
        var saved = await assertCtx.TourConfigs.FindAsync("newtour", "room1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.Tier, Is.EqualTo("OU"));
            Assert.That(saved.TourName, Is.EqualTo("New Tour"));
        }
    }

    [Test]
    public async Task Test_SaveTourConfigAsync_ShouldUpdateExistingConfig_WhenConfigAlreadyExists()
    {
        using (var setupCtx = new BotDbContext(_dbOptions))
        {
            setupCtx.TourConfigs.Add(new TourConfig
            {
                Id = "tour1", RoomId = "room1", Tier = "OU", Format = "elim",
                Autostart = 5, TourName = "Old Name"
            });
            await setupCtx.SaveChangesAsync();
        }

        var updated = new TourConfig
        {
            Id = "tour1", RoomId = "room1", Tier = "UU", Format = "roundrobin",
            Autostart = 20, TourName = "New Name", AutoDq = 3
        };

        await _sut.SaveTourConfigAsync(updated);

        using var assertCtx = new BotDbContext(_dbOptions);
        var saved = await assertCtx.TourConfigs.FindAsync("tour1", "room1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.Tier, Is.EqualTo("UU"));
            Assert.That(saved.Format, Is.EqualTo("roundrobin"));
            Assert.That(saved.Autostart, Is.EqualTo(20));
            Assert.That(saved.TourName, Is.EqualTo("New Name"));
            Assert.That(saved.AutoDq, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Test_DeleteTourConfigAsync_ShouldDoNothing_WhenConfigDoesNotExist()
    {
        Assert.DoesNotThrowAsync(() => _sut.DeleteTourConfigAsync("nonexistent", "room1"));
    }

    [Test]
    public async Task Test_DeleteTourConfigAsync_ShouldRemoveConfig_WhenConfigExists()
    {
        using (var setupCtx = new BotDbContext(_dbOptions))
        {
            setupCtx.TourConfigs.Add(new TourConfig
            {
                Id = "tour1", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 5
            });
            await setupCtx.SaveChangesAsync();
        }

        await _sut.DeleteTourConfigAsync("tour1", "room1");

        using var assertCtx = new BotDbContext(_dbOptions);
        var deleted = await assertCtx.TourConfigs.FindAsync("tour1", "room1");
        Assert.That(deleted, Is.Null);
    }

    [Test]
    public async Task Test_DeleteTourConfigAsync_ShouldNotDeleteOtherRoomConfig_WhenRoomIdDiffers()
    {
        using (var setupCtx = new BotDbContext(_dbOptions))
        {
            setupCtx.TourConfigs.AddRange(
                new TourConfig { Id = "tour1", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 5 },
                new TourConfig { Id = "tour1", RoomId = "room2", Tier = "OU", Format = "elim", Autostart = 5 }
            );
            await setupCtx.SaveChangesAsync();
        }

        await _sut.DeleteTourConfigAsync("tour1", "room1");

        using var assertCtx = new BotDbContext(_dbOptions);
        var room2Config = await assertCtx.TourConfigs.FindAsync("tour1", "room2");
        Assert.That(room2Config, Is.Not.Null);
    }
}
