using ElsaMina.Commands.Economy;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Economy;

public class MoneyServiceTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IRoomUserDataService _roomUserDataService;
    private MoneyService _sut;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _roomUserDataService = Substitute.For<IRoomUserDataService>();

        _sut = new MoneyService(_dbContextFactory, _roomUserDataService);
    }

    private async Task SeedRoomUserAsync(string userId, string roomId, long money)
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        dbContext.RoomUsers.Add(new RoomUser { Id = userId, RoomId = roomId, Money = money });
        await dbContext.SaveChangesAsync();
    }

    [Test]
    public async Task Test_GetBalanceAsync_ShouldReturnDefaultBalance_WhenUserHasNoData()
    {
        var balance = await _sut.GetBalanceAsync("room", "alice");

        Assert.That(balance, Is.EqualTo(IMoneyService.DEFAULT_BALANCE));
    }

    [Test]
    public async Task Test_GetBalanceAsync_ShouldReturnStoredBalance_WhenUserHasData()
    {
        await SeedRoomUserAsync("alice", "room", 450);

        var balance = await _sut.GetBalanceAsync("room", "alice");

        Assert.That(balance, Is.EqualTo(450));
    }

    [Test]
    public async Task Test_GetBalanceAsync_ShouldReturnDefaultBalance_WhenUserOnlyHasDataInAnotherRoom()
    {
        await SeedRoomUserAsync("alice", "otherroom", 450);

        var balance = await _sut.GetBalanceAsync("room", "alice");

        Assert.That(balance, Is.EqualTo(IMoneyService.DEFAULT_BALANCE));
    }

    [Test]
    public async Task Test_AddAsync_ShouldEnsureRoomUserExists()
    {
        await SeedRoomUserAsync("alice", "room", 100);

        await _sut.AddAsync("room", "alice", 10);

        await _roomUserDataService.Received(1)
            .GetOrCreateRoomSpecificUserDataAsync("room", "alice", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_AddAsync_ShouldIncreaseBalanceAndPersist_WhenAmountIsPositive()
    {
        await SeedRoomUserAsync("alice", "room", 100);

        var newBalance = await _sut.AddAsync("room", "alice", 50);

        await using var dbContext = new BotDbContext(_dbOptions);
        var roomUser = await dbContext.RoomUsers.FindAsync("alice", "room");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(newBalance, Is.EqualTo(150));
            Assert.That(roomUser.Money, Is.EqualTo(150));
        }
    }

    [Test]
    public async Task Test_AddAsync_ShouldDecreaseBalance_WhenAmountIsNegative()
    {
        await SeedRoomUserAsync("alice", "room", 100);

        var newBalance = await _sut.AddAsync("room", "alice", -30);

        Assert.That(newBalance, Is.EqualTo(70));
    }
}
