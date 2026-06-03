using ElsaMina.Commands;
using ElsaMina.Commands.Games.Semantix;
using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Semantix;

[TestFixture]
public class SemantixDailyServiceTest
{
    private IClockService _clockService;
    private IDataManager _dataManager;
    private IBotDbContextFactory _dbContextFactory;
    private SemantixDailyService _sut;

    [SetUp]
    public void SetUp()
    {
        _clockService = Substitute.For<IClockService>();
        _dataManager = Substitute.For<IDataManager>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();

        _dataManager.SemantixAnswersFr.Returns(["pomme", "maison", "chien", "soleil", "musique"]);
        _dataManager.SemantixWordsFr.Returns(["pomme", "maison", "chien", "soleil", "musique", "chat"]);

        _sut = new SemantixDailyService(_clockService, _dataManager, _dbContextFactory);
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldBeDeterministic_ForSameDay()
    {
        _clockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 15));

        var first = _sut.GetDailyAnswer();
        var second = _sut.GetDailyAnswer();

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first, Is.Not.Null);
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldChange_BetweenDays()
    {
        _clockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 15));
        var dayOne = _sut.GetDailyAnswer();

        _clockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 16));
        var dayTwo = _sut.GetDailyAnswer();

        Assert.That(dayOne, Is.Not.EqualTo(dayTwo));
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldReturnNull_WhenNoAnswersAvailable()
    {
        _dataManager.SemantixAnswersFr.Returns((IReadOnlyList<string>)null);
        _clockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 15));

        Assert.That(_sut.GetDailyAnswer(), Is.Null);
    }

    [Test]
    public void Test_IsValidWord_ShouldReturnTrue_WhenWordIsInList()
    {
        Assert.That(_sut.IsValidWord("chat"), Is.True);
    }

    [Test]
    public void Test_IsValidWord_ShouldReturnFalse_WhenWordIsNotInList()
    {
        Assert.That(_sut.IsValidWord("dfgdfg"), Is.False);
    }
}
