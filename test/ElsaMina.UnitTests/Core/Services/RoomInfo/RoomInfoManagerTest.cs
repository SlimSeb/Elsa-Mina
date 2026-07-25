using ElsaMina.Core;
using ElsaMina.Core.Services.RoomInfo;
using ElsaMina.Core.Services.System;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.RoomInfo;

public class RoomInfoManagerTest
{
    private IClient _client;
    private ISystemService _systemService;

    private RoomInfoManager _roomInfoManager;

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        _systemService = Substitute.For<ISystemService>();

        _roomInfoManager = new RoomInfoManager(_client, _systemService);
    }

    [Test]
    public async Task Test_GetRoomInfoAsync_ShouldReturnResolvedTask_WhenRoomInfoIsReceived()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(tcs.Task);
        var task = _roomInfoManager.GetRoomInfoAsync("franais");
        _roomInfoManager.HandleReceivedRoomInfo(
            """{"id":"franais","roomid":"franais","title":"Français","type":"chat","visibility":"public","modchat":null,"modjoin":null,"auth":{"#":["lionyx"],"@":["teclis"]},"users":["@Teclis","+Panur"]}""");

        // Act
        var result = await task;
        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(result.RoomId, Is.EqualTo("franais"));
            Assert.That(result.Title, Is.EqualTo("Français"));
            Assert.That(result.Type, Is.EqualTo("chat"));
            Assert.That(result.Visibility, Is.EqualTo("public"));
            Assert.That(result.Modchat, Is.Null);
            Assert.That(result.Auth["#"], Is.EqualTo(new[] { "lionyx" }));
            Assert.That(result.Users, Is.EqualTo(new[] { "@Teclis", "+Panur" }));
        }
    }

    [Test]
    public async Task Test_GetRoomInfoAsync_ShouldReturnNull_WhenRoomInfoIsNotReceived()
    {
        // Arrange
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _roomInfoManager.GetRoomInfoAsync("lobby");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRoomInfoAsync_ShouldSendCorrectCommand_ToClient()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(tcs.Task);
        var task = _roomInfoManager.GetRoomInfoAsync(" Lobby ");
        _roomInfoManager.HandleReceivedRoomInfo("""{"id":"lobby","roomid":"lobby","title":"Lobby"}""");

        // Act
        await task;

        // Assert
        _client.Received(1).Send("|/cmd roominfo lobby");
    }

    [Test]
    public async Task Test_HandleReceivedRoomInfo_ShouldParseModjoin_WhenModjoinIsSyncedWithModchat()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(tcs.Task);
        var task = _roomInfoManager.GetRoomInfoAsync("lobby");
        _roomInfoManager.HandleReceivedRoomInfo(
            """{"id":"lobby","roomid":"lobby","title":"Lobby","modchat":"+","modjoin":true}""");

        // Act
        var result = await task;

        // Assert
        Assert.That(result.Modjoin, Is.EqualTo("true"));
    }

    [Test]
    public async Task Test_HandleReceivedRoomInfo_ShouldResolveWithError_WhenRoomIsNotFound()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(tcs.Task);
        var task = _roomInfoManager.GetRoomInfoAsync("unknownroom");
        _roomInfoManager.HandleReceivedRoomInfo("""{"id":"unknownroom","error":"Room \"unknownroom\" not found."}""");

        // Act
        var result = await task;
        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.RoomId, Is.Null);
        }
    }

    [Test]
    public void Test_HandleReceivedRoomInfo_ShouldNotThrow_WhenJsonIsMalformed()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _roomInfoManager.HandleReceivedRoomInfo("not valid json {{"));
    }

    [Test]
    public void Test_HandleReceivedRoomInfo_ShouldNotThrow_WhenMessageIsEmpty()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _roomInfoManager.HandleReceivedRoomInfo(string.Empty));
    }
}
