using ElsaMina.Commands.Tournaments.Handlers;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Formats;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Tournaments.Handlers;

public class OtherRoomTournamentAnnounceHandlerTest
{
    private IFormatsManager _formatsManager;
    private IEventAnnouncer _eventAnnouncer;
    private OtherRoomTournamentAnnounceHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _formatsManager = Substitute.For<IFormatsManager>();
        _eventAnnouncer = Substitute.For<IEventAnnouncer>();

        _formatsManager.GetCleanFormat(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());

        _handler = new OtherRoomTournamentAnnounceHandler(_formatsManager, _eventAnnouncer);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenPartsAreTooShort()
    {
        var parts = new[] { "", "tournament", "create" };

        await _handler.HandleReceivedMessageAsync(parts, "sourceroom");

        _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenMessageTypeIsNotTournament()
    {
        var parts = new[] { "", "chat", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "sourceroom");

        _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenSubtypeIsNotCreate()
    {
        var parts = new[] { "", "tournament", "update", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "sourceroom");

        _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldCallGetCleanFormat_WithFormatFromParts()
    {
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _formatsManager.Received(1).GetCleanFormat("gen9ou");
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAnnounceTournamentEventWithCleanFormatAndSourceRoom_WhenTournamentCreated()
    {
        _formatsManager.GetCleanFormat("gen9ou").Returns("[Gen 9] OU");
        var parts = new[] { "", "tournament", "create", "gen9ou" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _eventAnnouncer.Received(1).AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Tournament,
            "tour_announce_message",
            Arg.Is<object[]>(arguments => arguments.Length == 2
                                          && (string)arguments[0] == "[Gen 9] OU"
                                          && (string)arguments[1] == "broadcastroom"),
            Arg.Any<CancellationToken>());
    }
}
