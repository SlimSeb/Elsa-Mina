using ElsaMina.Commands.Games;
using ElsaMina.Core.Services.EventAnnounces;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games;

public class HangmanAnnounceHandlerTest
{
    private static readonly string[] HANGMAN_100_PARTS = ["", "uhtml", "hangman100", "<div></div>"];
    private static readonly string[] HANGMAN_50_PARTS = ["", "uhtml", "hangman50", "<div></div>"];
    private static readonly string[] HANGMAN_1_PARTS = ["", "uhtml", "hangman1", "<div></div>"];
    private static readonly string[] HANGMAN_2_PARTS = ["", "uhtml", "hangman2", "<div></div>"];

    private IEventAnnouncer _eventAnnouncer;
    private HangmanAnnounceHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _eventAnnouncer = Substitute.For<IEventAnnouncer>();
        _handler = new HangmanAnnounceHandler(_eventAnnouncer);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenMessageTypeIsNotUhtml()
    {
        var parts = new[] { "", "chat", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "someroom");

        _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenUhtmlIdDoesNotContainHangman()
    {
        var parts = new[] { "", "uhtml", "poll123", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "someroom");

        _eventAnnouncer.DidNotReceiveWithAnyArgs()
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAnnounceGameEvent_WhenValidHangmanUhtmlReceived()
    {
        var parts = new[] { "", "uhtml", "hangman1", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _eventAnnouncer.Received(1).AnnounceToLinkedRoomsAsync("broadcastroom", EventAnnounceType.Game,
            "hangman_started_in",
            Arg.Is<object[]>(arguments => arguments.Length == 1 && (string)arguments[0] == "broadcastroom"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAnnounceOnce_WhenSameHangmanIdReceivedAgain()
    {
        var parts = new[] { "", "uhtml", "hangman2568", "<div></div>" };

        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");
        await _handler.HandleReceivedMessageAsync(parts, "broadcastroom");

        _eventAnnouncer.ReceivedWithAnyArgs(1)
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAnnounceOnce_WhenHangmanIdIsLowerThanLastId()
    {
        await _handler.HandleReceivedMessageAsync(HANGMAN_100_PARTS, "broadcastroom");
        await _handler.HandleReceivedMessageAsync(HANGMAN_50_PARTS, "broadcastroom");

        _eventAnnouncer.ReceivedWithAnyArgs(1)
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAnnounceAgain_WhenNewHangmanIdIsHigher()
    {
        await _handler.HandleReceivedMessageAsync(HANGMAN_1_PARTS, "broadcastroom");
        await _handler.HandleReceivedMessageAsync(HANGMAN_2_PARTS, "broadcastroom");

        _eventAnnouncer.ReceivedWithAnyArgs(2)
            .AnnounceToLinkedRoomsAsync(default, default, default, default);
    }
}
