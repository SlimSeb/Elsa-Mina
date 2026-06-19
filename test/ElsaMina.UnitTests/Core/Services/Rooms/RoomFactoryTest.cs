using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Rooms;

public class RoomFactoryTest
{
    private RoomFactory _sut;
    private IConfiguration _configuration;
    private IBotDbContextFactory _dbContextFactory;
    private IDependencyContainerService _dependencyContainerService;
    private IRoomParameterStore _roomParameterStore;
    private DbContextOptions<BotDbContext> _dbContextOptions;

    private IBotDbContextFactory CreateFactory(DbContextOptions<BotDbContext> options)
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(options)));
        return factory;
    }

    [SetUp]
    public void SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _configuration = Substitute.For<IConfiguration>();
        _configuration.DefaultLocaleCode.Returns("en-US");

        _dbContextFactory = CreateFactory(_dbContextOptions);

        _roomParameterStore = Substitute.For<IRoomParameterStore>();
        _roomParameterStore.GetValueAsync(Parameter.Locale, Arg.Any<CancellationToken>())
            .Returns("fr-FR");
        _roomParameterStore.GetValueAsync(Parameter.TimeZone, Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _dependencyContainerService.Resolve<IRoomParameterStore>().Returns(_roomParameterStore);

        _sut = new RoomFactory(_configuration, _dbContextFactory, _dependencyContainerService);
    }

    [Test]
    public async Task Test_CreateRoomAsync_WhenRoomIsNew_ShouldInsertSavedRoomAndReturnRoom()
    {
        // Arrange
        const string roomId = "newroom";
        string[] lines = ["|title|New Room", "|users|2,@Mod, Regular"];

        // Act
        var room = await _sut.CreateRoomAsync(roomId, lines);

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        var dbRoom = await assertContext.RoomInfo.SingleOrDefaultAsync(r => r.Id == roomId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(room, Is.Not.Null);
            Assert.That(room.Name, Is.EqualTo("New Room"));
            Assert.That(room.RoomId, Is.EqualTo(roomId));
            Assert.That(dbRoom, Is.Not.Null);
            Assert.That(dbRoom.Title, Is.EqualTo("New Room"));
        }
    }

    [Test]
    public async Task Test_CreateRoomAsync_WhenRoomExists_ShouldUpdateTitle()
    {
        // Arrange
        const string roomId = "existingroom";
        await using (var seedContext = new BotDbContext(_dbContextOptions))
        {
            seedContext.RoomInfo.Add(new SavedRoom { Id = roomId, Title = "Old Title" });
            await seedContext.SaveChangesAsync();
        }

        string[] lines = [$"|title|Updated Title", "|users|,user1"];

        // Act
        var room = await _sut.CreateRoomAsync(roomId, lines);

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        var dbRoom = await assertContext.RoomInfo.SingleOrDefaultAsync(r => r.Id == roomId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(room.Name, Is.EqualTo("Updated Title"));
            Assert.That(dbRoom.Title, Is.EqualTo("Updated Title"));
        }
    }

    [Test]
    public async Task Test_CreateRoomAsync_WhenNoTitleLine_ShouldUseRoomIdAsTitle()
    {
        // Arrange
        const string roomId = "notitleroom";
        string[] lines = ["|users|,user1"];

        // Act
        var room = await _sut.CreateRoomAsync(roomId, lines);

        // Assert
        Assert.That(room.Name, Is.EqualTo(roomId));
    }

    [Test]
    public async Task Test_CreateRoomAsync_WhenLocaleIsNull_ShouldFallBackToDefaultLocaleCode()
    {
        // Arrange
        const string roomId = "testroom";
        _roomParameterStore.GetValueAsync(Parameter.Locale, Arg.Any<CancellationToken>())
            .Returns((string)null);
        _configuration.DefaultLocaleCode.Returns("it-IT");
        string[] lines = ["|title|Test Room"];

        // Act
        var room = await _sut.CreateRoomAsync(roomId, lines);

        // Assert
        Assert.That(room.Culture.Name, Is.EqualTo("it-IT"));
    }

    [Test]
    public async Task Test_CreateRoomAsync_ShouldInitializeParameterStoreFromDbEntity()
    {
        // Arrange
        const string roomId = "testroom";
        string[] lines = ["|title|Test Room"];

        // Act
        await _sut.CreateRoomAsync(roomId, lines);

        // Assert
        _roomParameterStore.Received(1).InitializeFromRoomEntity(Arg.Is<SavedRoom>(r => r.Id == roomId));
    }

    [Test]
    public async Task Test_CreateRoomAsync_ShouldAssignRoomToParameterStore()
    {
        // Arrange
        const string roomId = "testroom";
        string[] lines = ["|title|Test Room"];

        // Act
        var room = await _sut.CreateRoomAsync(roomId, lines);

        // Assert
        Assert.That(_roomParameterStore.Room, Is.SameAs(room));
    }

    [Test]
    public async Task Test_CreateRoomAsync_ShouldPopulateUsers()
    {
        // Arrange
        const string roomId = "testroom";
        string[] lines = ["|title|Test Room", "|users|2,@Moderator, RegularUser"];

        // Act
        var room = await _sut.CreateRoomAsync(roomId, lines);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(room.Users.ContainsKey("moderator"), Is.True);
            Assert.That(room.Users.ContainsKey("regularuser"), Is.True);
        }
    }

    [Test]
    public async Task Test_CreateRoomAsync_WhenValidTimeZone_ShouldApplyTimeZone()
    {
        // Arrange
        const string roomId = "testroom";
        const string timeZoneId = "Europe/Paris";
        _roomParameterStore.GetValueAsync(Parameter.TimeZone, Arg.Any<CancellationToken>())
            .Returns(timeZoneId);
        string[] lines = ["|title|Test Room"];

        // Act
        var room = await _sut.CreateRoomAsync(roomId, lines);

        // Assert
        Assert.That(room.TimeZone.Id, Is.EqualTo(timeZoneId));
    }

    [Test]
    public async Task Test_CreateRoomAsync_WhenInvalidTimeZone_ShouldFallBackToLocalTimeZone()
    {
        // Arrange
        const string roomId = "testroom";
        _roomParameterStore.GetValueAsync(Parameter.TimeZone, Arg.Any<CancellationToken>())
            .Returns("Not/AReal/TimeZone");
        string[] lines = ["|title|Test Room"];

        // Act
        var room = await _sut.CreateRoomAsync(roomId, lines);

        // Assert
        Assert.That(room.TimeZone, Is.EqualTo(TimeZoneInfo.Local));
    }
}
