using System.Globalization;
using ElsaMina.Commands;
using ElsaMina.Commands.Games.Wordle;
using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Wordle;

public class WordleDailyServiceTest
{
    private static readonly CultureInfo English = new("en-US");
    private static readonly CultureInfo French = new("fr-FR");

    private WordleDailyService _service;
    private IClockService _mockClockService;
    private IDataManager _mockDataManager;
    private IBotDbContextFactory _mockDbContextFactory;

    [SetUp]
    public void SetUp()
    {
        _mockClockService = Substitute.For<IClockService>();
        _mockDataManager = Substitute.For<IDataManager>();
        _mockDbContextFactory = Substitute.For<IBotDbContextFactory>();
        _mockDataManager.WordleWords.Returns(new List<string> { "apple", "crane", "level", "ghost" });
        _mockDataManager.WordleWordsFr.Returns(new List<string> { "avion", "blanc", "carte", "champ" });

        _service = new WordleDailyService(_mockClockService, _mockDataManager, _mockDbContextFactory);
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldBeStable_ForTheSameDay()
    {
        // Arrange
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc));

        // Act
        var first = _service.GetDailyAnswer(English);
        var second = _service.GetDailyAnswer(English);

        // Assert
        Assert.That(first, Is.EqualTo(second));
        Assert.That(first, Is.Not.Empty);
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldNotDependOnTimeOfDay()
    {
        // Arrange
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 0, 1, 0, DateTimeKind.Utc));
        var morning = _service.GetDailyAnswer(English);
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 23, 59, 0, DateTimeKind.Utc));

        // Act
        var night = _service.GetDailyAnswer(English);

        // Assert
        Assert.That(morning, Is.EqualTo(night));
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldChange_FromOneDayToTheNext()
    {
        // Arrange
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        var day1 = _service.GetDailyAnswer(English);
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var day2 = _service.GetDailyAnswer(English);

        // Assert
        Assert.That(day1, Is.Not.EqualTo(day2));
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldReturnAnUppercaseWordFromTheList()
    {
        // Arrange
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var answer = _service.GetDailyAnswer(English);

        // Assert
        Assert.That(answer, Is.EqualTo(answer.ToUpperInvariant()));
        Assert.That(new[] { "APPLE", "CRANE", "LEVEL", "GHOST" }, Does.Contain(answer));
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldUseFrenchList_WhenCultureIsFrench()
    {
        // Arrange
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var answer = _service.GetDailyAnswer(French);

        // Assert
        Assert.That(new[] { "AVION", "BLANC", "CARTE", "CHAMP" }, Does.Contain(answer));
    }

    [Test]
    public void Test_GetWords_ShouldReturnEnglishList_WhenCultureIsNotFrench()
    {
        // Act
        var words = _service.GetWords(English);

        // Assert
        Assert.That(words, Is.EqualTo(new[] { "apple", "crane", "level", "ghost" }));
    }

    [Test]
    public void Test_GetWords_ShouldReturnFrenchList_WhenCultureIsFrench()
    {
        // Act
        var words = _service.GetWords(French);

        // Assert
        Assert.That(words, Is.EqualTo(new[] { "avion", "blanc", "carte", "champ" }));
    }
}
