using System.Globalization;
using ElsaMina.Commands.Tournaments;
using ElsaMina.Commands.Tournaments.Handlers;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Formats;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Tournaments.Handlers;

public class OtherRoomTournamentAnnounceHandlerTest
{
    private static readonly string[] RECEIVING_ROOMS = ["receivingroom"];
    private static readonly string[] MULTIPLE_RECEIVING_ROOMS = ["room1", "room2", "room3"];
    private static readonly string[] RECEIVER_A = ["receiver-a"];
    private static readonly string[] RECEIVER_B = ["receiver-b"];

    private IConfiguration _configuration;
    private IBot _bot;
    private IFormatsManager _formatsManager;
    private IResourcesService _resourcesService;
    private IRoomsManager _roomsManager;
    private OtherRoomTournamentAnnounceHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _bot = Substitute.For<IBot>();
        _formatsManager = Substitute.For<IFormatsManager>();
        _resourcesService = Substitute.For<IResourcesService>();
        _roomsManager = Substitute.For<IRoomsManager>();

        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>());
        _configuration.DefaultLocaleCode.Returns("en-US");
        _formatsManager.GetCleanFormat(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());
        _resourcesService.GetString(Arg.Any<string>(), Arg.Any<CultureInfo>()).Returns("Tournament announced: {0} in {1}");

        _handler = new OtherRoomTournamentAnnounceHandler(
            _configuration, _bot, _formatsManager, _resourcesService, _roomsManager);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenPartsAreTooShort()
    {
        var parts = new[] { "", "tournament", "create" };

        await _handler.HandleReceivedMessageAsync(parts, "sourceroom");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenMessageTypeIsNotTournament()
    {
        var parts = new[] { "", "chat", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "sourceroom");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenSubtypeIsNotCreate()
    {
        var parts = new[] { "", "tournament", "update", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "sourceroom");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenTourAnnouncesIsEmpty()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>());
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "sourceroom");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenRoomIdDoesNotMatchBroadcastingRoom()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "otherroom");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSayToReceivingRoom_WhenRoomIdMatchesBroadcastingRoom()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _bot.Received(1).Say("receivingroom", Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSayToAllReceivingRooms_WhenMultipleReceivingRoomsConfigured()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", MULTIPLE_RECEIVING_ROOMS }
        });
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _bot.Received(1).Say("room1", Arg.Any<string>());
        _bot.Received(1).Say("room2", Arg.Any<string>());
        _bot.Received(1).Say("room3", Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldCallGetCleanFormat_WithFormatFromParts()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _formatsManager.Received(1).GetCleanFormat("gen9ou");
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldUseRoomCulture_WhenReceivingRoomExists()
    {
        var roomCulture = new CultureInfo("fr-FR");
        var room = Substitute.For<IRoom>();
        room.Culture.Returns(roomCulture);
        _roomsManager.GetRoom("receivingroom").Returns(room);
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _resourcesService.Received(1).GetString("tour_announce_message", roomCulture);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldUseDefaultLocale_WhenReceivingRoomDoesNotExist()
    {
        _roomsManager.GetRoom("receivingroom").Returns((IRoom)null);
        _configuration.DefaultLocaleCode.Returns("en-US");
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _resourcesService.Received(1).GetString("tour_announce_message",
            Arg.Is<CultureInfo>(culture => culture.Name == "en-US"));
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIncludeCleanFormatInMessage_WhenSending()
    {
        _formatsManager.GetCleanFormat("gen9ou").Returns("[Gen 9] OU");
        _resourcesService.GetString("tour_announce_message", Arg.Any<CultureInfo>())
            .Returns("A tournament in {0} was announced in {1}!");
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _bot.Received(1).Say("receivingroom",
            "/wall A tournament in [Gen 9] OU was announced in broadcastroom!");
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldOnlySendToMatchingBroadcastRoom_WhenMultipleBroadcastRoomsConfigured()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "room-a", RECEIVER_A },
            { "room-b", RECEIVER_B }
        });
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "room-a");

        _bot.Received(1).Say("receiver-a", Arg.Any<string>());
        _bot.DidNotReceive().Say("receiver-b", Arg.Any<string>());
    }
}
