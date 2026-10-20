using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.UnitTests.Core.Services.Authorization;

public class PermissionGrantTest
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Test]
    [TestCase("+", ExpectedResult = Rank.Voiced)]
    [TestCase("%", ExpectedResult = Rank.Driver)]
    [TestCase("@", ExpectedResult = Rank.Mod)]
    [TestCase("#", ExpectedResult = Rank.Mod)]
    [TestCase("~", ExpectedResult = Rank.Mod)]
    [TestCase("x", ExpectedResult = Rank.Regular)]
    [TestCase("", ExpectedResult = Rank.Regular)]
    public Rank Test_GrantedRank_ShouldReturnRankCappedAtMaximum_WhenGrantIsARank(string target)
    {
        return CreateGrant(PermissionGrantKind.Rank, target).GrantedRank;
    }

    [Test]
    public void Test_GrantedRank_ShouldReturnRegular_WhenGrantIsNotARank()
    {
        Assert.That(CreateGrant(PermissionGrantKind.Command, "%").GrantedRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    [TestCase(PermissionGrantKind.Rank, "+", ExpectedResult = true)]
    [TestCase(PermissionGrantKind.Command, "%", ExpectedResult = false)]
    public bool Test_IsFor_ShouldMatchAnyRank_WhenGrantIsARank(PermissionGrantKind kind, string target)
    {
        return CreateGrant(PermissionGrantKind.Rank, "%").IsFor(kind, target);
    }

    [Test]
    [TestCase("add-badge", ExpectedResult = true)]
    [TestCase("ADD-BADGE", ExpectedResult = true)]
    [TestCase("givebadge", ExpectedResult = false)]
    public bool Test_IsFor_ShouldCompareTargetIgnoringCase_WhenGrantIsACommand(string target)
    {
        return CreateGrant(PermissionGrantKind.Command, "add-badge").IsFor(PermissionGrantKind.Command, target);
    }

    [Test]
    public void Test_IsActiveAt_ShouldBeTrueUntilExpiry()
    {
        // Arrange
        var temporary = CreateGrant(PermissionGrantKind.Rank, "%") with { ExpiresAt = NOW.AddHours(1) };
        var permanent = CreateGrant(PermissionGrantKind.Rank, "%");

        // Act & Assert
        Assert.That(temporary.IsActiveAt(NOW), Is.True);
        Assert.That(temporary.IsActiveAt(NOW.AddHours(1)), Is.False);
        Assert.That(permanent.IsActiveAt(DateTimeOffset.MaxValue), Is.True);
    }

    private static PermissionGrant CreateGrant(PermissionGrantKind kind, string target) =>
        new("room", "alice", kind, target, "owner", NOW, null);
}
