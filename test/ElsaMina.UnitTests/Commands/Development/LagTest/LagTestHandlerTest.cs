using ElsaMina.Commands.Development.LagTest;
using ElsaMina.Core.Services.Config;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Development.LagTest;

[TestFixture]
public class LagTestHandlerTest
{
    private IConfiguration _configuration;
    private ILagTestManager _lagTestManager;
    private LagTestHandler _handler;

    private static readonly string[] EXPECTED_MESSAGE_TYPES = ["c:"];

    private const string BOT_NAME = "ElsaMina";
    private const string TEST_ROOM_ID = "testroom";

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _lagTestManager = Substitute.For<ILagTestManager>();

        _configuration.Name.Returns(BOT_NAME);

        _handler = new LagTestHandler(_configuration, _lagTestManager);
    }

    [Test]
    public void Test_HandledMessageTypes_ShouldBeCorrect()
    {
        Assert.That(_handler.HandledMessageTypes, Is.EqualTo(EXPECTED_MESSAGE_TYPES));
    } 
    
    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnore_WhenPartsAreTooShort()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(["", "c:", "12345", "elsamina"], TEST_ROOM_ID);

        // Assert
        _lagTestManager.DidNotReceive().HandleEcho(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnore_WhenMessageTypeIsNotChat()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(
            ["", "pm", "12345", "elsamina", LagTestManager.LAG_TEST_MARKER],
            TEST_ROOM_ID);

        // Assert
        _lagTestManager.DidNotReceive().HandleEcho(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnore_WhenSenderIsNotBot()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(
            ["", "c:", "12345", "someotheruser", LagTestManager.LAG_TEST_MARKER],
            TEST_ROOM_ID);

        // Assert
        _lagTestManager.DidNotReceive().HandleEcho(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnore_WhenMessageIsNotLagTestMarker()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(
            ["", "c:", "12345", "elsamina", "some unrelated message"],
            TEST_ROOM_ID);

        // Assert
        _lagTestManager.DidNotReceive().HandleEcho(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldResolveEcho_WhenBotSendsMarker()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(
            ["", "c:", "12345", "elsamina", LagTestManager.LAG_TEST_MARKER],
            TEST_ROOM_ID);

        // Assert
        _lagTestManager.Received(1).HandleEcho(TEST_ROOM_ID);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldMatchBotNameCaseInsensitively()
    {
        // Arrange
        _configuration.Name.Returns("ElsaMina");

        // Act
        await _handler.HandleReceivedMessageAsync(
            ["", "c:", "12345", "ELSAMINA", LagTestManager.LAG_TEST_MARKER],
            TEST_ROOM_ID);

        // Assert
        _lagTestManager.Received(1).HandleEcho(TEST_ROOM_ID);
    }
}
