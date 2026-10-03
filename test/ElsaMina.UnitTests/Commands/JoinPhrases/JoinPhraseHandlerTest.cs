using ElsaMina.Commands.JoinPhrases;
using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.JoinPhrases;

public class JoinPhraseHandlerTest
{
    private IRoomUserDataService _roomUserDataService;
    private IBot _bot;
    private IClockService _clockService;
    private JoinPhraseHandler _handler;

    private const string TEST_ROOM_ID = "testroom";
    private const string TEST_USER_ID = "testuser";
    private const string JOIN_PHRASE = "Welcome back!";

    [SetUp]
    public void SetUp()
    {
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _bot = Substitute.For<IBot>();
        _clockService = Substitute.For<IClockService>();

        _handler = new JoinPhraseHandler(_roomUserDataService, _bot, _clockService);
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldNotHandle_WhenPartsAreInvalid()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(["invalid", "message"], TEST_ROOM_ID);

        // Assert
        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldNotHandle_WhenMessageTypeIsNotJoin()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(["", "L", TEST_USER_ID], TEST_ROOM_ID);

        // Assert
        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldNotTriggerPhrase_WhenUserHasNoJoinPhrase()
    {
        // Arrange
        _roomUserDataService.JoinPhrases.TryGetValue(Arg.Any<Tuple<string, string>>(), out _).Returns(false);

        // Act
        await _handler.HandleReceivedMessageAsync(["", "J", TEST_USER_ID], TEST_ROOM_ID);

        // Assert
        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldTriggerPhrase_WhenValidJoinAndCooldownPassed()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _clockService.CurrentUtcDateTime.Returns(now);
        var key = Tuple.Create(TEST_USER_ID, TEST_ROOM_ID);
        _roomUserDataService.JoinPhrases.Returns(new Dictionary<Tuple<string, string>, string>
        {
            [key] = JOIN_PHRASE
        });

        // Act
        await _handler.HandleReceivedMessageAsync(["", "J", TEST_USER_ID], TEST_ROOM_ID);

        // Assert
        _bot.Received(1).Say(TEST_ROOM_ID, JOIN_PHRASE);
    }
}
