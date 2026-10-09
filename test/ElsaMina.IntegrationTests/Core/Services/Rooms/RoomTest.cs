using System.Globalization;
using ElsaMina.Commands;
using ElsaMina.Commands.Users.PlayTimes;
using ElsaMina.Commands.Users.Seen;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Core.Services.Rooms;

public class RoomTest
{
    private const string ROOM_ID = "franais";

    private BotDbContextFactory _dbContextFactory;
    private RoomsManager _roomsManager;
    private PlayTimeUpdateService _playTimeUpdateService;
    private IUserSaveQueue _userSaveQueue;

    [SetUp]
    public void SetUp()
    {
        var dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = new BotDbContextFactory(new PooledDbContextFactory<BotDbContext>(dbOptions));

        var configuration = Substitute.For<ICommandsConfiguration>();
        configuration.DefaultLocaleCode.Returns("fr-FR");
        configuration.PlayTimeUpdatesInterval.Returns(TimeSpan.FromDays(1));

        var resourcesService = Substitute.For<IResourcesService>();
        resourcesService.SupportedCultures.Returns([
            new CultureInfo("fr-FR"),
            new CultureInfo("en-US")
        ]);

        var parametersFactory =
            new ParametersDefinitionFactory([new CoreRoomParameters(configuration, resourcesService)]);
        var roomParameterRepository = new EfRoomParameterRepository(_dbContextFactory);

        _userSaveQueue = Substitute.For<IUserSaveQueue>();
        _userSaveQueue.AcquireLockAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var roomFactory = new RoomFactory(configuration, roomParameterRepository,
            () => new RoomParameterStore(roomParameterRepository, parametersFactory));

        _roomsManager = new RoomsManager(roomFactory);

        _playTimeUpdateService = new PlayTimeUpdateService(configuration, _dbContextFactory, _userSaveQueue,
            _roomsManager);
    }

    [TearDown]
    public async Task TearDown()
    {
        _playTimeUpdateService.Dispose();
        await _userSaveQueue.DisposeAsync();
    }

    [Test]
    public async Task Test_InitializeRoomAsync_ShouldCreateRoomWithUsers_AndPersistRoom()
    {
        await _roomsManager.InitializeRoomAsync(ROOM_ID, BuildRoomInitLines());

        var room = _roomsManager.GetRoom(ROOM_ID);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var persistedRoom = await dbContext.RoomInfo.SingleAsync(x => x.Id == ROOM_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(room, Is.Not.Null);
            Assert.That(room.Name, Is.EqualTo("Français"));
            Assert.That(room.Users.ContainsKey("earth"), Is.True);
            Assert.That(room.Users.ContainsKey("mec"), Is.True);
            Assert.That(persistedRoom.Title, Is.EqualTo("Français"));
        }
    }

    [Test]
    public async Task Test_RoomConfiguration_ShouldPersistLocaleParameter()
    {
        await _roomsManager.InitializeRoomAsync(ROOM_ID, BuildRoomInitLines());
        var room = _roomsManager.GetRoom(ROOM_ID);

        Assert.That(await room.GetParameterValueAsync(Parameter.Locale), Is.EqualTo("fr-FR"));

        var updateResult = await room.SetParameterValueAsync(Parameter.Locale, "en-US");
        Assert.That(updateResult, Is.True);

        _roomsManager.Clear();

        await _roomsManager.InitializeRoomAsync(ROOM_ID, BuildRoomInitLines());
        var reloadedRoom = _roomsManager.GetRoom(ROOM_ID);
        var reloadedLocale = await reloadedRoom.GetParameterValueAsync(Parameter.Locale);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloadedLocale, Is.EqualTo("en-US"));
            Assert.That(reloadedRoom.Culture.Name, Is.EqualTo("en-US"));
        }
    }

    [Test]
    public async Task Test_RoomUserMutations_ShouldAddRenameAndRemoveUsers()
    {
        await _roomsManager.InitializeRoomAsync(ROOM_ID, BuildRoomInitLines());
        var room = _roomsManager.GetRoom(ROOM_ID);

        _roomsManager.AddUserToRoom(ROOM_ID, "+NewUser");
        _roomsManager.RenameUserInRoom(ROOM_ID, "+NewUser", "@RenamedUser");
        _roomsManager.RemoveUserFromRoom(ROOM_ID, "@RenamedUser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(room.Users.ContainsKey("newuser"), Is.False);
            Assert.That(room.Users.ContainsKey("renameduser"), Is.False);
        }
    }

    [Test]
    public async Task Test_ProcessPendingPlayTimeUpdates_ShouldPersistPlayTime()
    {
        await _roomsManager.InitializeRoomAsync(ROOM_ID, BuildRoomInitLines());
        var room = _roomsManager.GetRoom(ROOM_ID);
        room.PendingPlayTimeUpdates["earth"] = TimeSpan.FromMinutes(5);

        await _playTimeUpdateService.ProcessPendingPlayTimeUpdatesAsync();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var roomUser = await dbContext.RoomUsers.SingleAsync(x => x.Id == "earth" && x.RoomId == ROOM_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(roomUser.PlayTime, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(room.PendingPlayTimeUpdates.ContainsKey("earth"), Is.False);
        }

        await _userSaveQueue.Received(1).AcquireLockAsync(Arg.Any<CancellationToken>());
        _userSaveQueue.Received(1).ReleaseLock();
    }

    private static string[] BuildRoomInitLines() =>
    [
        $">{ROOM_ID}",
        "|init|chat",
        "|title|Français",
        "|users|4,&Teclis,!Lionyx,@Earth, Mec"
    ];
}
