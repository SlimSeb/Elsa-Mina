using ElsaMina.Commands.Misc.RandomImages;

namespace ElsaMina.UnitTests.Commands.Misc.RandomImages;

[TestFixture]
public class TenorCooldownServiceTest
{
    private TenorCooldownService _service;

    [SetUp]
    public void SetUp()
    {
        _service = new TenorCooldownService();
    }

    [Test]
    public void Test_GetRemainingCooldowns_ShouldReturnZeroForBoth_WhenNoCooldownSet()
    {
        var (roomRemaining, userRemaining) = _service.GetRemainingCooldowns("room1", "user1", DateTimeOffset.UtcNow);

        Assert.That(roomRemaining, Is.EqualTo(TimeSpan.Zero));
        Assert.That(userRemaining, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Test_GetRemainingCooldowns_ShouldReturnRoomRemaining_WhenRoomIsOnCooldown()
    {
        var now = DateTimeOffset.UtcNow;
        _service.SetCooldown("room1", "user1", now);

        var (roomRemaining, _) = _service.GetRemainingCooldowns("room1", "user2", now.AddSeconds(30));

        Assert.That(roomRemaining,
            Is.EqualTo(TenorConstants.PER_ROOM_COOLDOWN - TimeSpan.FromSeconds(30))
                .Within(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public void Test_GetRemainingCooldowns_ShouldReturnZeroForRoom_WhenRoomCooldownHasExpired()
    {
        var now = DateTimeOffset.UtcNow;
        _service.SetCooldown("room1", "user1", now);

        var (roomRemaining, _) =
            _service.GetRemainingCooldowns("room1", "user2", now + TenorConstants.PER_ROOM_COOLDOWN + TimeSpan.FromSeconds(1));

        Assert.That(roomRemaining, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Test_GetRemainingCooldowns_ShouldReturnUserRemaining_WhenUserIsOnCooldown()
    {
        var now = DateTimeOffset.UtcNow;
        _service.SetCooldown("room1", "user1", now);

        var (_, userRemaining) = _service.GetRemainingCooldowns("room2", "user1", now.AddMinutes(5));

        Assert.That(userRemaining,
            Is.EqualTo(TenorConstants.PER_USER_COOLDOWN - TimeSpan.FromMinutes(5))
                .Within(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public void Test_GetRemainingCooldowns_ShouldReturnZeroForUser_WhenUserCooldownHasExpired()
    {
        var now = DateTimeOffset.UtcNow;
        _service.SetCooldown("room1", "user1", now);

        var (_, userRemaining) =
            _service.GetRemainingCooldowns("room2", "user1", now + TenorConstants.PER_USER_COOLDOWN + TimeSpan.FromSeconds(1));

        Assert.That(userRemaining, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Test_GetRemainingCooldowns_ShouldReturnZeroForRoom_WhenDifferentRoomQueried()
    {
        var now = DateTimeOffset.UtcNow;
        _service.SetCooldown("room1", "user1", now);

        var (roomRemaining, _) = _service.GetRemainingCooldowns("room2", "user2", now);

        Assert.That(roomRemaining, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Test_GetRemainingCooldowns_ShouldReturnZeroForUser_WhenDifferentUserQueried()
    {
        var now = DateTimeOffset.UtcNow;
        _service.SetCooldown("room1", "user1", now);

        var (_, userRemaining) = _service.GetRemainingCooldowns("room2", "user2", now);

        Assert.That(userRemaining, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Test_GetRemainingCooldowns_ShouldReturnBothRemainingTimes_WhenBothAreOnCooldown()
    {
        var now = DateTimeOffset.UtcNow;
        _service.SetCooldown("room1", "user1", now);

        var elapsed = TimeSpan.FromSeconds(30);
        var (roomRemaining, userRemaining) = _service.GetRemainingCooldowns("room1", "user1", now + elapsed);

        Assert.That(roomRemaining,
            Is.EqualTo(TenorConstants.PER_ROOM_COOLDOWN - elapsed).Within(TimeSpan.FromMilliseconds(1)));
        Assert.That(userRemaining,
            Is.EqualTo(TenorConstants.PER_USER_COOLDOWN - elapsed).Within(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public void Test_SetCooldown_ShouldOverwriteExistingCooldown_WhenCalledAgain()
    {
        var now = DateTimeOffset.UtcNow;
        _service.SetCooldown("room1", "user1", now);

        var resetTime = now.AddMinutes(10);
        _service.SetCooldown("room1", "user1", resetTime);

        var checkTime = resetTime.AddSeconds(30);
        var (roomRemaining, userRemaining) = _service.GetRemainingCooldowns("room1", "user1", checkTime);

        Assert.That(roomRemaining,
            Is.EqualTo(TenorConstants.PER_ROOM_COOLDOWN - TimeSpan.FromSeconds(30))
                .Within(TimeSpan.FromMilliseconds(1)));
        Assert.That(userRemaining,
            Is.EqualTo(TenorConstants.PER_USER_COOLDOWN - TimeSpan.FromSeconds(30))
                .Within(TimeSpan.FromMilliseconds(1)));
    }
}
