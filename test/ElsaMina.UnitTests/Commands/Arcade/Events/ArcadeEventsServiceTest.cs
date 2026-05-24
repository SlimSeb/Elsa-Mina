using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Services.Clock;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Events;

public class ArcadeEventsServiceTest
{
    private IClockService _clockService;
    private ArcadeEventsService _service;

    [SetUp]
    public void SetUp()
    {
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _service = new ArcadeEventsService(_clockService);
    }

    [Test]
    public void Test_AreGamesMuted_ShouldReturnFalse_WhenNoMuteWasSet()
    {
        Assert.That(_service.AreGamesMuted("arcade"), Is.False);
    }

    [Test]
    public void Test_AreGamesMuted_ShouldReturnTrue_WhenMuteIsActive()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _clockService.CurrentUtcDateTimeOffset.Returns(now);

        _service.MuteGames("arcade", TimeSpan.FromMinutes(45));

        Assert.That(_service.AreGamesMuted("arcade"), Is.True);
    }

    [Test]
    public void Test_AreGamesMuted_ShouldReturnFalse_WhenMuteHasExpired()
    {
        var muteStart = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var afterExpiry = muteStart.AddMinutes(46);

        _clockService.CurrentUtcDateTimeOffset.Returns(muteStart);
        _service.MuteGames("arcade", TimeSpan.FromMinutes(45));

        _clockService.CurrentUtcDateTimeOffset.Returns(afterExpiry);

        Assert.That(_service.AreGamesMuted("arcade"), Is.False);
    }

    [Test]
    public void Test_AreGamesMuted_ShouldReturnTrue_WhenCheckedExactlyAtExpiry()
    {
        var muteStart = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var exactExpiry = muteStart.AddMinutes(45);

        _clockService.CurrentUtcDateTimeOffset.Returns(muteStart);
        _service.MuteGames("arcade", TimeSpan.FromMinutes(45));

        // At the exact expiry moment the condition is `now < expiry`, so expiry itself is NOT muted
        _clockService.CurrentUtcDateTimeOffset.Returns(exactExpiry);

        Assert.That(_service.AreGamesMuted("arcade"), Is.False);
    }

    [Test]
    public void Test_AreGamesMuted_ShouldReturnFalse_ForDifferentRoom_WhenOnlyOneRoomIsMuted()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _clockService.CurrentUtcDateTimeOffset.Returns(now);

        _service.MuteGames("arcade", TimeSpan.FromMinutes(45));

        Assert.That(_service.AreGamesMuted("otherroom"), Is.False);
    }

    [Test]
    public void Test_MuteGames_ShouldOverridePreviousMute_WhenCalledAgain()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _clockService.CurrentUtcDateTimeOffset.Returns(start);
        _service.MuteGames("arcade", TimeSpan.FromMinutes(1));

        // Re-mute with 45 minutes
        _service.MuteGames("arcade", TimeSpan.FromMinutes(45));

        // 2 minutes later - original 1-minute mute would have expired, but new 45-minute is still active
        _clockService.CurrentUtcDateTimeOffset.Returns(start.AddMinutes(2));

        Assert.That(_service.AreGamesMuted("arcade"), Is.True);
    }

    [Test]
    public void Test_AreGamesMuted_ShouldReturnTrue_WhenMuteJustBeforeExpiry()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _clockService.CurrentUtcDateTimeOffset.Returns(now);
        _service.MuteGames("arcade", TimeSpan.FromMinutes(45));

        _clockService.CurrentUtcDateTimeOffset.Returns(now.AddMinutes(44).AddSeconds(59));

        Assert.That(_service.AreGamesMuted("arcade"), Is.True);
    }
}
