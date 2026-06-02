using System.Globalization;
using ElsaMina.Commands.Games;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games;

public class HangmanAnnounceHandlerTest
{
    private static readonly string[] RECEIVING_ROOMS = ["receivingroom"];
    private static readonly string[] MULTIPLE_RECEIVING_ROOMS = ["room1", "room2", "room3"];
    private static readonly string[] RECEIVER_A = ["receiver-a"];
    private static readonly string[] RECEIVER_B = ["receiver-b"];
    private static readonly string[] HANGMAN_100_PARTS = ["", "uhtml", "hangman100", "<div></div>"];
    private static readonly string[] HANGMAN_50_PARTS = ["", "uhtml", "hangman50", "<div></div>"];
    private static readonly string[] HANGMAN_1_PARTS = ["", "uhtml", "hangman1", "<div></div>"];
    private static readonly string[] HANGMAN_2_PARTS = ["", "uhtml", "hangman2", "<div></div>"];

    private IConfiguration _configuration;
    private IBot _bot;
    private IRoomsManager _roomsManager;
    private IResourcesService _resourcesService;
    private HangmanAnnounceHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _bot = Substitute.For<IBot>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _resourcesService = Substitute.For<IResourcesService>();

        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>());
        _configuration.DefaultLocaleCode.Returns("en-US");
        _resourcesService.GetString(Arg.Any<string>(), Arg.Any<CultureInfo>())
            .Returns("A new hangman game was created in <<{0}>>");

        _handler = new HangmanAnnounceHandler(_configuration, _bot, _roomsManager, _resourcesService);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenMessageTypeIsNotUhtml()
    {
        var parts = new[] { "", "chat", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "someroom");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenUhtmlIdDoesNotContainHangman()
    {
        var parts = new[] { "", "uhtml", "poll123", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "someroom");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenEventAnnouncesIsEmpty()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>());
        var parts = new[] { "", "uhtml", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "someroom");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenRoomIdDoesNotMatchBroadcastingRoom()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "uhtml", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "otherroom");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenSameHangmanIdReceivedAgain()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "uhtml", "hangman2568", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");
        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _bot.Received(1).Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenHangmanIdIsLowerThanLastId()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _handler.HandleReceivedMessageAsync(HANGMAN_100_PARTS, "broadcastroom");
        await _handler.HandleReceivedMessageAsync(HANGMAN_50_PARTS, "broadcastroom");

        _bot.Received(1).Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSayToReceivingRoom_WhenRoomIdMatchesBroadcastingRoom()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "uhtml", "hangman1", "<div></div>" };

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
        var parts = new[] { "", "uhtml", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _bot.Received(1).Say("room1", Arg.Any<string>());
        _bot.Received(1).Say("room2", Arg.Any<string>());
        _bot.Received(1).Say("room3", Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldOnlySayToMatchingBroadcastRoom_WhenMultipleBroadcastRoomsConfigured()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "room-a", RECEIVER_A },
            { "room-b", RECEIVER_B }
        });
        var parts = new[] { "", "uhtml", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "room-a");

        _bot.Received(1).Say("receiver-a", Arg.Any<string>());
        _bot.DidNotReceive().Say("receiver-b", Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAnnounceAgain_WhenNewHangmanIdIsHigher()
    {
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });

        await _handler.HandleReceivedMessageAsync(HANGMAN_1_PARTS, "broadcastroom");
        await _handler.HandleReceivedMessageAsync(HANGMAN_2_PARTS, "broadcastroom");

        _bot.Received(2).Say("receivingroom", Arg.Any<string>());
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
        var parts = new[] { "", "uhtml", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _resourcesService.Received(1).GetString("hangman_started_in", roomCulture);
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
        var parts = new[] { "", "uhtml", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _resourcesService.Received(1).GetString("hangman_started_in",
            Arg.Is<CultureInfo>(culture => culture.Name == "en-US"));
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIncludeBroadcastingRoomInMessage_WhenSending()
    {
        _resourcesService.GetString("hangman_started_in", Arg.Any<CultureInfo>())
            .Returns("A new hangman game was created in <<{0}>>");
        _configuration.EventAnnounces.Returns(new Dictionary<string, IEnumerable<string>>
        {
            { "broadcastroom", RECEIVING_ROOMS }
        });
        var parts = new[] { "", "uhtml", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _bot.Received(1).Say("receivingroom", "/wall A new hangman game was created in <<broadcastroom>>");
    }
}
