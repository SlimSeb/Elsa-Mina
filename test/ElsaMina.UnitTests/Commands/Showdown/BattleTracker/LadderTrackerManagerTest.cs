using System.Globalization;
using ElsaMina.Commands.Showdown.BattleTracker;
using ElsaMina.Core;
using ElsaMina.Core.Services.BattleTracker;
using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Formats;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Showdown.BattleTracker;

public class LadderTrackerManagerTest
{
    private IActiveBattlesManager _activeBattlesManager;
    private IBot _bot;
    private IRoomsManager _roomsManager;
    private IResourcesService _resourcesService;
    private IFormatsManager _formatsManager;
    private IDependencyContainerService _previousDependencyContainerService;
    private LadderTrackerManager _manager;

    [SetUp]
    public void SetUp()
    {
        _activeBattlesManager = Substitute.For<IActiveBattlesManager>();
        _bot = Substitute.For<IBot>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _resourcesService = Substitute.For<IResourcesService>();
        _formatsManager = Substitute.For<IFormatsManager>();

        var room = Substitute.For<IRoom>();
        room.Culture.Returns(CultureInfo.InvariantCulture);
        _roomsManager.GetRoom(Arg.Any<string>()).Returns(room);
        _resourcesService.GetString("battletracker_battle_started", Arg.Any<CultureInfo>())
            .Returns("{0} {1} {2} {3}");
        _formatsManager.GetCleanFormat(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());

        _previousDependencyContainerService = DependencyContainerService.Current;
        var dependencyContainerService = Substitute.For<IDependencyContainerService>();
        var customColorsManager = Substitute.For<ICustomColorsManager>();
        customColorsManager.CustomColorsMapping.Returns(new Dictionary<string, string>());
        dependencyContainerService.Resolve<ICustomColorsManager>().Returns(customColorsManager);
        DependencyContainerService.Current = dependencyContainerService;

        _manager = new LadderTrackerManager(_activeBattlesManager, _bot,
            _roomsManager, _resourcesService, _formatsManager, TimeSpan.FromMilliseconds(25));
    }

    [TearDown]
    public void TearDown()
    {
        _manager.Dispose();
        DependencyContainerService.Current = _previousDependencyContainerService;
    }

    [Test]
    public async Task Test_StartTracking_ShouldBroadcastToAllSubscribedRooms_WhenNewBattleIsDetected()
    {
        // Arrange
        var pollCount = 0;
        _activeBattlesManager.GetActiveBattlesAsync("gen9ou", 0, "prefix", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                pollCount++;
                return Task.FromResult<IReadOnlyCollection<ActiveBattleDto>>(pollCount switch
                {
                    1 => [BuildBattle("battle-gen9ou-2544193700", "alice", "bob", 1200)],
                    _ => [
                        BuildBattle("battle-gen9ou-2544193700", "alice", "bob", 1200),
                        BuildBattle("battle-gen9ou-2544193713", "charlie", "david", 1337)
                    ]
                });
            });

        // Act
        _manager.StartTracking("room-a", "gen9ou", "prefix");
        _manager.StartTracking("room-b", "gen9ou", "prefix");
        await Task.Delay(TimeSpan.FromMilliseconds(250));

        // Assert
        _bot.Received(1).Say("room-a",
            Arg.Is<string>(message => message.Contains("/battle-gen9ou-2544193713")
                                      && message.Contains("charlie")
                                      && message.Contains("david")
                                      && message.Contains("1337")));
        _bot.Received(1).Say("room-b",
            Arg.Is<string>(message => message.Contains("/battle-gen9ou-2544193713")
                                      && message.Contains("charlie")
                                      && message.Contains("david")
                                      && message.Contains("1337")));
    }

    [Test]
    public async Task Test_StartTracking_ShouldNotBroadcastExistingBattles_OnFirstSnapshot()
    {
        // Arrange
        var pollCount = 0;

        _activeBattlesManager.GetActiveBattlesAsync("gen9ou", 0, "prefix", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                pollCount++;
                return Task.FromResult<IReadOnlyCollection<ActiveBattleDto>>(
                [
                    BuildBattle("battle-gen9ou-2544193713", "alice", "bob", 1200)
                ]);
            });

        // Act
        _manager.StartTracking("room-a", "gen9ou", "prefix");
        await Task.Delay(TimeSpan.FromMilliseconds(80));

        // Assert
        Assert.That(pollCount, Is.GreaterThan(0));
        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_StopTracking_ShouldStopPolling_WhenNoTrackingIsOngoing()
    {
        // Arrange
        var pollCount = 0;
        _activeBattlesManager.GetActiveBattlesAsync("gen9ou", 0, "prefix", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref pollCount);
                return Task.FromResult<IReadOnlyCollection<ActiveBattleDto>>(Array.Empty<ActiveBattleDto>());
            });

        // Act
        _manager.StartTracking("room-a", "gen9ou", "prefix");
        await Task.Delay(TimeSpan.FromMilliseconds(80));
        _manager.StopTracking("room-a", "gen9ou", "prefix");
        var pollCountAfterStop = pollCount;
        await Task.Delay(TimeSpan.FromMilliseconds(80));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pollCountAfterStop, Is.GreaterThan(0));
            Assert.That(pollCount, Is.LessThanOrEqualTo(pollCountAfterStop + 1));
        }
    }

    [Test]
    public async Task Test_StopTracking_ShouldKeepPolling_WhenOneSubscribedRoomStillRemains()
    {
        // Arrange
        var pollCount = 0;
        _activeBattlesManager.GetActiveBattlesAsync("gen9ou", 0, "prefix", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref pollCount);
                return Task.FromResult<IReadOnlyCollection<ActiveBattleDto>>(Array.Empty<ActiveBattleDto>());
            });

        // Act
        _manager.StartTracking("room-a", "gen9ou", "prefix");
        _manager.StartTracking("room-b", "gen9ou", "prefix");
        await Task.Delay(TimeSpan.FromMilliseconds(80));

        _manager.StopTracking("room-a", "gen9ou", "prefix");
        var pollCountAfterFirstUnsubscribe = pollCount;
        await Task.Delay(TimeSpan.FromMilliseconds(80));
        var pollCountWithOneRoomLeft = pollCount;

        _manager.StopTracking("room-b", "gen9ou", "prefix");
        var pollCountAfterFinalUnsubscribe = pollCount;
        await Task.Delay(TimeSpan.FromMilliseconds(80));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pollCountAfterFirstUnsubscribe, Is.GreaterThan(0));
            Assert.That(pollCountWithOneRoomLeft, Is.GreaterThan(pollCountAfterFirstUnsubscribe));
            Assert.That(pollCount, Is.LessThanOrEqualTo(pollCountAfterFinalUnsubscribe + 1));
        }
    }

    private static ActiveBattleDto BuildBattle(string roomId, string player1, string player2, int elo)
    {
        return new ActiveBattleDto
        {
            RoomId = roomId,
            Player1 = player1,
            Player2 = player2,
            MinElo = elo
        };
    }
}
