using ElsaMina.Core.Services.EventAnnounces;

namespace ElsaMina.UnitTests.Core.Services.EventAnnounces;

public class EventAnnouncesTypeValuesTest
{
    [TestCase(EventAnnouncesTypeValues.All, EventAnnounceType.Tournament, true)]
    [TestCase(EventAnnouncesTypeValues.All, EventAnnounceType.Game, true)]
    [TestCase(EventAnnouncesTypeValues.TournamentsOnly, EventAnnounceType.Tournament, true)]
    [TestCase(EventAnnouncesTypeValues.TournamentsOnly, EventAnnounceType.Game, false)]
    [TestCase(EventAnnouncesTypeValues.GamesOnly, EventAnnounceType.Tournament, false)]
    [TestCase(EventAnnouncesTypeValues.GamesOnly, EventAnnounceType.Game, true)]
    [TestCase(EventAnnouncesTypeValues.None, EventAnnounceType.Tournament, false)]
    [TestCase(EventAnnouncesTypeValues.None, EventAnnounceType.Game, false)]
    public void Test_Allows_ShouldReturnExpected_ForConfiguredValue(string storedValue, EventAnnounceType announceType,
        bool expected)
    {
        Assert.That(EventAnnouncesTypeValues.Allows(storedValue, announceType), Is.EqualTo(expected));
    }

    [TestCase(EventAnnounceType.Tournament)]
    [TestCase(EventAnnounceType.Game)]
    public void Test_Allows_ShouldFallBackToAllowingEverything_ForUnknownValue(EventAnnounceType announceType)
    {
        Assert.That(EventAnnouncesTypeValues.Allows("some-legacy-value", announceType), Is.True);
        Assert.That(EventAnnouncesTypeValues.Allows(null, announceType), Is.True);
    }
}
