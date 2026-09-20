using ElsaMina.Core.Utils;

namespace ElsaMina.UnitTests.Core.Utils;

public class RomanNumeralSuffixComparerTest
{
    private RomanNumeralSuffixComparer _comparer;

    [SetUp]
    public void SetUp()
    {
        _comparer = new RomanNumeralSuffixComparer();
    }

    [Test]
    [TestCase("Cup I", "Cup II", ExpectedResult = -1)]
    [TestCase("Cup IV", "Cup IX", ExpectedResult = -1)]
    [TestCase("Cup IX", "Cup V", ExpectedResult = 1)]
    [TestCase("Cup X", "Cup IX", ExpectedResult = 1)]
    [TestCase("Cup XL", "Cup L", ExpectedResult = -1)]
    public int Test_Compare_ShouldCompareNumerically_WhenPrefixesAreEqual(string first, string second)
    {
        // Act
        var result = _comparer.Compare(first, second);

        // Assert
        return Math.Sign(result);
    }

    [Test]
    public void Test_Compare_ShouldPrioritizeNumeral_WhenPrefixesDiffer()
    {
        // Arrange
        const string firstEditionOfLaterSeries = "Vainqueur DPP Cup I";
        const string secondEditionOfEarlierSeries = "Vainqueur ADV Cup II";

        // Act
        var result = _comparer.Compare(firstEditionOfLaterSeries, secondEditionOfEarlierSeries);

        // Assert
        Assert.That(result, Is.LessThan(0));
    }

    [Test]
    public void Test_Compare_ShouldFallBackToPrefix_WhenNumeralsAreEqual()
    {
        // Arrange
        const string first = "Vainqueur ADV Cup II";
        const string second = "Vainqueur BW Cup II";

        // Act
        var result = _comparer.Compare(first, second);

        // Assert
        Assert.That(result, Is.LessThan(0));
    }

    [Test]
    [TestCase("French Frontier", "French Frontier II", ExpectedResult = -1)]
    [TestCase("Vainqueur ADV Cup", "French Frontier", ExpectedResult = 1)]
    [TestCase("French Frontier", "Vainqueur ADV Cup I", ExpectedResult = -1)]
    public int Test_Compare_ShouldTreatMissingNumeralAsZero(string first, string second)
    {
        // Act
        var result = _comparer.Compare(first, second);

        // Assert
        return Math.Sign(result);
    }

    [Test]
    [TestCase("Winner VGC", "Winner VGC")]
    [TestCase("Winner OLD", "Winner OLD")]
    public void Test_Compare_ShouldIgnoreNumeralLetters_WhenTheyAreNotAStandaloneWord(string first, string second)
    {
        // Act
        var result = _comparer.Compare(first, second);

        // Assert
        Assert.That(result, Is.Zero);
    }

    [Test]
    public void Test_Compare_ShouldIgnoreSuffix_WhenNumeralIsNotCanonical()
    {
        // Arrange, "IIII" is not a canonical numeral, so both names sort as numeral zero
        const string nonCanonical = "Cup IIII";
        const string unnumbered = "Cup";

        // Act
        var result = _comparer.Compare(nonCanonical, unnumbered);

        // Assert
        Assert.That(result, Is.GreaterThan(0));
    }

    [Test]
    public void Test_Compare_ShouldBeCaseInsensitiveOnNumerals()
    {
        // Act
        var result = _comparer.Compare("Cup iv", "Cup V");

        // Assert
        Assert.That(result, Is.LessThan(0));
    }

    [Test]
    public void Test_Compare_ShouldStillReadNumeral_WhenStringEndsWithWhitespace()
    {
        // Act
        var result = _comparer.Compare("Cup II  ", "Cup III");

        // Assert
        Assert.That(result, Is.LessThan(0));
    }

    [Test]
    public void Test_Compare_ShouldOrderDeterministically_WhenNamesOnlyDifferByCase()
    {
        // Arrange
        const string lowerCase = "cup II";
        const string upperCase = "Cup II";

        // Act
        var result = _comparer.Compare(lowerCase, upperCase);

        // Assert
        Assert.That(result, Is.Not.Zero);
        Assert.That(Math.Sign(result), Is.EqualTo(-Math.Sign(_comparer.Compare(upperCase, lowerCase))));
    }

    [Test]
    [TestCase(null, null, ExpectedResult = 0)]
    [TestCase(null, "Cup I", ExpectedResult = -1)]
    [TestCase("Cup I", null, ExpectedResult = 1)]
    [TestCase("", "", ExpectedResult = 0)]
    public int Test_Compare_ShouldHandleNullAndEmptyStrings(string first, string second)
    {
        // Act
        var result = _comparer.Compare(first, second);

        // Assert
        return Math.Sign(result);
    }

    [Test]
    public void Test_Compare_ShouldOrderABadgeList_WhenUsedAsAnOrderByComparer()
    {
        // Arrange
        string[] badgeNames =
        [
            "Vainqueur BW Cup II",
            "Vainqueur ADV Cup X",
            "French Frontier",
            "Vainqueur ADV Cup II",
            "Vainqueur ADV Cup I",
            "Vainqueur BW Cup I"
        ];

        // Act
        var result = badgeNames.OrderBy(name => name, _comparer).ToArray();

        // Assert
        Assert.That(result, Is.EqualTo([
            "French Frontier",
            "Vainqueur ADV Cup I",
            "Vainqueur BW Cup I",
            "Vainqueur ADV Cup II",
            "Vainqueur BW Cup II",
            "Vainqueur ADV Cup X"
        ]));
    }
}
