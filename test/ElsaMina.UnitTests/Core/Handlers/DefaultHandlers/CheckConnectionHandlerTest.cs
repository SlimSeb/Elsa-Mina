using ElsaMina.Core;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.System;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Handlers.DefaultHandlers;

public class CheckConnectionHandlerTest
{
    private IConfiguration _configuration;
    private IClient _client;
    private ISystemService _systemService;

    private CheckConnectionHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _client = Substitute.For<IClient>();
        _systemService = Substitute.For<ISystemService>();

        _handler = new CheckConnectionHandler(_configuration, _client, _systemService);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldJoinRooms_WhenConnectionHasBeenVeritified()
    {
        // Arrange
        string[] message = ["", "updateuser", "+LeBot", "1", "1", "{}"];
        _configuration.Name.Returns("LeBot");
        _configuration.Rooms.Returns(["botdev", "franais", "lobby"]);
        _configuration.RoomBlacklist.Returns(["lobby"]);

        // Act
        await _handler.HandleReceivedMessageAsync(message);

        // Assert
        await _client.Received(1).SendAsync("|/join botdev", Arg.Any<CancellationToken>());
        await _client.Received(1).SendAsync("|/join franais", Arg.Any<CancellationToken>());
        await _client.DidNotReceive().SendAsync("|/join lobby", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenIsConnectedAsGuest()
    {
        // Arrange
        string[] message = ["", "updateuser", " Guest 123", "1", "1", "{}"];
        _configuration.Name.Returns("LeBot");
        _configuration.Rooms.Returns(["botdev", "franais", "lobby"]);
        _configuration.RoomBlacklist.Returns(["lobby"]);

        // Act
        await _handler.HandleReceivedMessageAsync(message);

        // Assert
        await _client.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [TestCase(null, 0)]
    [TestCase("", 0)]
    [TestCase("avy", 1)]
    public async Task Test_HandleReceivedMessageAsync_ShouldSetAvatar_WhenAvatarIsDefinedInConfiguration(string avatar,
        int expectedCalls)
    {
        // Arrange
        string[] message = ["", "updateuser", "+LeBot", "1", "1", "{}"];
        _configuration.Name.Returns("LeBot");
        _configuration.Avatar.Returns(avatar);
        
        // Act
        await _handler.HandleReceivedMessageAsync(message);
        
        // Assert
        await _client.Received(expectedCalls).SendAsync($"|/avatar {avatar}", Arg.Any<CancellationToken>());
    }
}