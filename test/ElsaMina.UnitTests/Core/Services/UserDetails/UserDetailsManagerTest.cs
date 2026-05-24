using ElsaMina.Core;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.UserDetails;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.UserDetails;

public class  UserDetailsManagerTest
{
    private IClient _client;
    private ISystemService _systemService;

    private UserDetailsManager _userDetailsManager;

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        _systemService = Substitute.For<ISystemService>();

        _userDetailsManager = new UserDetailsManager(_client, _systemService);
    }

    [Test]
    public async Task Test_GetUserDetails_ShouldReturnTaskResolved_WhenUserDetailsAreReceived()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(tcs.Task);
        var task = _userDetailsManager.GetUserDetailsAsync("panur");
        _userDetailsManager.HandleReceivedUserDetails("""{"id":"panur","userid":"panur","name":"Panur","avatar":"sightseerf","group":"+","autoconfirmed":true}""");
        
        // Act
        var result = await task;
        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(result.Name, Is.EqualTo("Panur"));
            Assert.That(result.Avatar, Is.EqualTo("sightseerf"));
            Assert.That(result.Group, Is.EqualTo("+"));
            Assert.That(result.AutoConfirmed, Is.True);
        }
    }

    [Test]
    public async Task Test_GetUserDetails_ShouldReturnNull_WhenUserDetailsAreNotReceived()
    {
        // Arrange
        // TODO : revoir ce test
        _systemService.SleepAsync(Arg.Any<TimeSpan>()).Returns(Task.Delay(TimeSpan.FromSeconds(1)));

        // Act
        var result = await _userDetailsManager.GetUserDetailsAsync("speks");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetUserDetailsAsync_ShouldSendCorrectCommand_ToClient()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(tcs.Task);
        var task = _userDetailsManager.GetUserDetailsAsync("panur");
        _userDetailsManager.HandleReceivedUserDetails("""{"userid":"panur","name":"Panur"}""");

        // Act
        await task;

        // Assert
        _client.Received(1).Send("|/cmd userdetails panur");
    }

    [Test]
    public async Task Test_HandleReceivedUserDetails_ShouldSetRoomsToNull_WhenPayloadContainsRoomsFalse()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(tcs.Task);
        var task = _userDetailsManager.GetUserDetailsAsync("panur");
        _userDetailsManager.HandleReceivedUserDetails("""{"userid":"panur","name":"Panur","rooms":false}""");

        // Act
        var result = await task;

        // Assert
        Assert.That(result.Rooms, Is.Null);
    }

    [Test]
    public async Task Test_HandleReceivedUserDetails_ShouldSetRoomsToNonNull_WhenPayloadContainsRoomData()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(tcs.Task);
        var task = _userDetailsManager.GetUserDetailsAsync("panur");
        _userDetailsManager.HandleReceivedUserDetails("""{"userid":"panur","name":"Panur","rooms":{" lobby":{},"franais":{}}}""");

        // Act
        var result = await task;

        // Assert
        Assert.That(result.Rooms, Is.Not.Null);
    }

    [Test]
    public void Test_HandleReceivedUserDetails_ShouldNotThrow_WhenJsonIsMalformed()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _userDetailsManager.HandleReceivedUserDetails("not valid json {{"));
    }

    [Test]
    public void Test_HandleReceivedUserDetails_ShouldNotThrow_WhenMessageIsEmpty()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _userDetailsManager.HandleReceivedUserDetails(string.Empty));
    }
}