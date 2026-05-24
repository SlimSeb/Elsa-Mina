using ElsaMina.Core;
using ElsaMina.Core.Services.BattleTracker;
using ElsaMina.Core.Services.System;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.BattleTracker;

public class ActiveBattlesManagerTest
{
    private IClient _client;
    private ISystemService _systemService;

    private ActiveBattlesManager _activeBattlesManager;

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        _systemService = Substitute.For<ISystemService>();

        _activeBattlesManager = new ActiveBattlesManager(_client, _systemService);
    }

    [Test]
    public async Task Test_GetActiveBattles_ShouldReturnTaskResolved_WhenRoomListIsReceived()
    {
        // Arrange
        var timeoutTask = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(timeoutTask.Task);
        var task = _activeBattlesManager.GetActiveBattlesAsync("gen9ou", 0, "prefix");

        _activeBattlesManager.HandleReceivedRoomList("""
                                                     {"rooms":{"battle-gen9ou-2544193713":{"p1":"shiyedesu","p2":"servantofthejudge","minElo":1273},"battle-gen9ou-2544192244":{"p1":"shinasiluque","p2":"sephiramemra","minElo":1178}}}
                                                     """);

        // Act
        var result = await task;

        // Assert
        _client.Received(1).Send("|/cmd roomlist gen9ou,none,prefix");
        Assert.That(result, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.First().RoomId, Is.EqualTo("battle-gen9ou-2544193713"));
            Assert.That(result.First().Player1, Is.EqualTo("shiyedesu"));
            Assert.That(result.First().Player2, Is.EqualTo("servantofthejudge"));
            Assert.That(result.First().MinElo, Is.EqualTo(1273));
        }
    }

    [Test]
    public async Task Test_GetActiveBattles_ShouldReturnEmpty_WhenRoomListIsNotReceived()
    {
        // Arrange
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.Delay(TimeSpan.FromMilliseconds(10)));

        // Act
        var result = await _activeBattlesManager.GetActiveBattlesAsync("gen9ou", 0, "prefix");

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_GetActiveBattles_ShouldUseProvidedFilters_WhenSendingRoomListCommand()
    {
        // Arrange
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.Delay(TimeSpan.FromMilliseconds(10)));

        // Act
        await _activeBattlesManager.GetActiveBattlesAsync("gen9ou", 1500, "my-prefix");

        // Assert
        _client.Received(1).Send("|/cmd roomlist gen9ou,1500,my-prefix");
    }
}
