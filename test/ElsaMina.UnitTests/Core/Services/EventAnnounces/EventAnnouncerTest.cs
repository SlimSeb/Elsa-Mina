using System.Globalization;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.EventAnnounces;

public class EventAnnouncerTest
{
    private static readonly string[] RECEIVING_ROOMS = ["receivingroom"];
    private static readonly string[] MULTIPLE_RECEIVING_ROOMS = ["room1", "room2", "room3"];
    private static readonly string[] RECEIVER_A = ["receiver-a"];
    private static readonly string[] RECEIVER_B = ["receiver-b"];

    private IConfiguration _configuration;
    private IBot _bot;
    private IResourcesService _resourcesService;
    private IRoomsManager _roomsManager;
    private IClockService _clockService;
    private EventAnnouncer _eventAnnouncer;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _bot = Substitute.For<IBot>();
        _resourcesService = Substitute.For<IResourcesService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _clockService = Substitute.For<IClockService>();

        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>());
        _configuration.DefaultLocaleCode.Returns("en-US");
        _resourcesService.GetString(Arg.Any<string>(), Arg.Any<CultureInfo>())
            .Returns("A new game was created in <<{0}>>");
        _clockService.CurrentUtcDateTime.Returns(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        _eventAnnouncer = new EventAnnouncer(_configuration, _bot, _resourcesService, _roomsManager, _clockService);
    }

    private static IRoom RoomWithAnnouncesFilter(string filterValue)
    {
        var room = Substitute.For<IRoom>();
        room.GetParameterValueAsync(Parameter.EventAnnouncesType, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(filterValue));
        return room;
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldDoNothing_WhenSourceRoomIdIsNull()
    {
        await _eventAnnouncer.AnnounceToLinkedRoomsAsync(null, EventAnnounceType.Game, "some_key", ["arg"]);

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldDoNothing_WhenEventAnnouncesIsEmpty()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>());

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("someroom", EventAnnounceType.Game, "some_key", ["someroom"]);

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldDoNothing_WhenSourceRoomIsNotABroadcastingRoom()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("otherroom", EventAnnounceType.Game, "some_key", ["otherroom"]);

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldSayToReceivingRoom_WhenSourceRoomIsABroadcastingRoom()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _bot.Received(1).Say("receivingroom", Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldSayToAllReceivingRooms_WhenMultipleReceivingRoomsConfigured()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", MULTIPLE_RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _bot.Received(1).Say("room1", Arg.Any<string>());
        _bot.Received(1).Say("room2", Arg.Any<string>());
        _bot.Received(1).Say("room3", Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldOnlySayToMatchingBroadcastRoom_WhenMultipleBroadcastRoomsConfigured()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "room-a", RECEIVER_A },
            { "room-b", RECEIVER_B }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("room-a", EventAnnounceType.Game, "some_key", ["room-a"]);

        _bot.Received(1).Say("receiver-a", Arg.Any<string>());
        _bot.DidNotReceive().Say("receiver-b", Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldUseRoomCulture_WhenReceivingRoomExists()
    {
        var roomCulture = new CultureInfo("fr-FR");
        var room = RoomWithAnnouncesFilter(EventAnnouncesTypeValues.All);
        room.Culture.Returns(roomCulture);
        _roomsManager.GetRoom("receivingroom").Returns(room);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _resourcesService.Received(1).GetString("some_key", roomCulture);
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldUseDefaultLocale_WhenReceivingRoomDoesNotExist()
    {
        _roomsManager.GetRoom("receivingroom").Returns((IRoom)null);
        _configuration.DefaultLocaleCode.Returns("en-US");
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _resourcesService.Received(1)
            .GetString("some_key", Arg.Is<CultureInfo>(culture => culture.Name == "en-US"));
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldFormatMessageWithArguments_WhenSending()
    {
        _resourcesService.GetString("tour_announce_message", Arg.Any<CultureInfo>())
            .Returns("A tournament in {0} was announced in {1}!");
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Tournament,
            "tour_announce_message", ["[Gen 9] OU", "broadcastroom"]);

        _bot.Received(1).Say("receivingroom", "/wall A tournament in [Gen 9] OU was announced in broadcastroom!");
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldSkipRoom_WhenRoomOnlyWantsTournamentsButAnnounceIsGame()
    {
        var room = RoomWithAnnouncesFilter(EventAnnouncesTypeValues.TournamentsOnly);
        _roomsManager.GetRoom("receivingroom").Returns(room);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldAnnounce_WhenRoomOnlyWantsTournamentsAndAnnounceIsTournament()
    {
        var room = RoomWithAnnouncesFilter(EventAnnouncesTypeValues.TournamentsOnly);
        _roomsManager.GetRoom("receivingroom").Returns(room);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Tournament, "some_key",
            ["broadcastroom"]);

        _bot.Received(1).Say("receivingroom", Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldSkipRoom_WhenRoomOnlyWantsGamesButAnnounceIsTournament()
    {
        var room = RoomWithAnnouncesFilter(EventAnnouncesTypeValues.GamesOnly);
        _roomsManager.GetRoom("receivingroom").Returns(room);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Tournament, "some_key",
            ["broadcastroom"]);

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldSkipRoom_WhenRoomWantsNoAnnounces()
    {
        var room = RoomWithAnnouncesFilter(EventAnnouncesTypeValues.None);
        _roomsManager.GetRoom("receivingroom").Returns(room);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldOnlySkipFilteredRooms_WhenReceivingRoomsHaveDifferentFilters()
    {
        var room1 = RoomWithAnnouncesFilter(EventAnnouncesTypeValues.All);
        var room2 = RoomWithAnnouncesFilter(EventAnnouncesTypeValues.TournamentsOnly);
        var room3 = RoomWithAnnouncesFilter(EventAnnouncesTypeValues.GamesOnly);
        _roomsManager.GetRoom("room1").Returns(room1);
        _roomsManager.GetRoom("room2").Returns(room2);
        _roomsManager.GetRoom("room3").Returns(room3);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", MULTIPLE_RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _bot.Received(1).Say("room1", Arg.Any<string>());
        _bot.DidNotReceive().Say("room2", Arg.Any<string>());
        _bot.Received(1).Say("room3", Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldNotAnnounceAgain_WhenWithinCooldown()
    {
        var startTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clockService.CurrentUtcDateTime.Returns(startTime);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _clockService.CurrentUtcDateTime.Returns(startTime.AddMinutes(29));
        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _bot.Received(1).Say("receivingroom", Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldAnnounceAgain_WhenCooldownHasElapsed()
    {
        var startTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clockService.CurrentUtcDateTime.Returns(startTime);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _clockService.CurrentUtcDateTime.Returns(startTime.AddMinutes(31));
        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);

        _bot.Received(2).Say("receivingroom", Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldTrackCooldownPerRoom_WhenMultipleRoomsAreLinked()
    {
        var startTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clockService.CurrentUtcDateTime.Returns(startTime);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "room-a", RECEIVER_A },
            { "room-b", RECEIVER_B }
        });

        // room-a is announced to and enters its cooldown.
        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("room-a", EventAnnounceType.Game, "some_key", ["room-a"]);

        // room-b has its own cooldown and should still be announced to within room-a's cooldown window.
        _clockService.CurrentUtcDateTime.Returns(startTime.AddMinutes(5));
        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("room-b", EventAnnounceType.Game, "some_key", ["room-b"]);

        _bot.Received(1).Say("receiver-a", Arg.Any<string>());
        _bot.Received(1).Say("receiver-b", Arg.Any<string>());
    }

    [Test]
    public async Task Test_AnnounceToLinkedRoomsAsync_ShouldNotStartCooldown_WhenAnnounceIsFilteredOut()
    {
        var startTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clockService.CurrentUtcDateTime.Returns(startTime);
        var room = RoomWithAnnouncesFilter(EventAnnouncesTypeValues.TournamentsOnly);
        _roomsManager.GetRoom("receivingroom").Returns(room);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        // A game announce is filtered out, so it must not consume the room's cooldown.
        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game, "some_key",
            ["broadcastroom"]);
        // A tournament announce moments later is allowed and should still go through.
        _clockService.CurrentUtcDateTime.Returns(startTime.AddMinutes(1));
        await _eventAnnouncer.AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Tournament, "some_key",
            ["broadcastroom"]);

        _bot.Received(1).Say("receivingroom", Arg.Any<string>());
    }
}
