using System.Globalization;
using ElsaMina.Commands;
using ElsaMina.Commands.Games.Wordle;
using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Wordle;

public class WordleDailyServiceTest
{
    private static readonly CultureInfo ENGLISH = new("en-US");
    private static readonly CultureInfo FRENCH = new("fr-FR");

    private static readonly string[] ENGLISH_ANSWERS = ["APPLE", "CRANE", "LEVEL", "GHOST"];
    private static readonly string[] FRENCH_ANSWERS = ["AVION", "BLANC", "CARTE", "CHAMP"];
    private static readonly string[] ENGLISH_WORDS = ["apple", "crane", "level", "ghost"];
    private static readonly string[] FRENCH_WORDS = ["avion", "blanc", "carte", "champ"];

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
        var first = _service.GetDailyAnswer(ENGLISH);
        var second = _service.GetDailyAnswer(ENGLISH);

        // Assert
        Assert.That(first, Is.EqualTo(second));
        Assert.That(first, Is.Not.Empty);
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldNotDependOnTimeOfDay()
    {
        // Arrange
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 0, 1, 0, DateTimeKind.Utc));
        var morning = _service.GetDailyAnswer(ENGLISH);
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 23, 59, 0, DateTimeKind.Utc));

        // Act
        var night = _service.GetDailyAnswer(ENGLISH);

        // Assert
        Assert.That(morning, Is.EqualTo(night));
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldChange_FromOneDayToTheNext()
    {
        // Arrange
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        var day1 = _service.GetDailyAnswer(ENGLISH);
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var day2 = _service.GetDailyAnswer(ENGLISH);

        // Assert
        Assert.That(day1, Is.Not.EqualTo(day2));
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldReturnAnUppercaseWordFromTheList()
    {
        // Arrange
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var answer = _service.GetDailyAnswer(ENGLISH);

        // Assert
        Assert.That(answer, Is.EqualTo(answer.ToUpperInvariant()));
        Assert.That(ENGLISH_ANSWERS, Does.Contain(answer));
    }

    [Test]
    public void Test_GetDailyAnswer_ShouldUseFrenchList_WhenCultureIsFrench()
    {
        // Arrange
        _mockClockService.CurrentUtcDateTime.Returns(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var answer = _service.GetDailyAnswer(FRENCH);

        // Assert
        Assert.That(FRENCH_ANSWERS, Does.Contain(answer));
    }

    [Test]
    public void Test_GetWords_ShouldReturnEnglishList_WhenCultureIsNotFrench()
    {
        // Act
        var words = _service.GetWords(ENGLISH);

        // Assert
        Assert.That(words, Is.EqualTo(ENGLISH_WORDS));
    }

    [Test]
    public void Test_GetWords_ShouldReturnFrenchList_WhenCultureIsFrench()
    {
        // Act
        var words = _service.GetWords(FRENCH);

        // Assert
        Assert.That(words, Is.EqualTo(FRENCH_WORDS));
    }
}
