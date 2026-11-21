using ElsaMina.Core.Services.Formats;

namespace ElsaMina.UnitTests.Core.Services.Formats;

public class FormatAliasesTest
{
    [TestCase("gen9ou", new[] { "gen9ou", "ou" })]
    [TestCase("ou", new[] { "ou", "gen9ou" })]
    [TestCase("gen91v1", new[] { "gen91v1", "1v1" })]
    [TestCase("gen9nationaldex", new[] { "gen9nationaldex", "nationaldex", "gen9natdex", "natdex" })]
    [TestCase("natdex", new[] { "natdex", "gen9nationaldex", "nationaldex", "gen9natdex" })]
    [TestCase("gen9almostanyability", new[] { "gen9almostanyability", "almostanyability", "gen9aaa", "aaa" })]
    [TestCase("gen9anythinggoes", new[] { "gen9anythinggoes", "anythinggoes", "gen9ag", "ag" })]
    [TestCase("gen8ou", new[] { "gen8ou" })]
    [TestCase("gen8nationaldex", new[] { "gen8nationaldex", "gen8natdex" })]
    [TestCase("gen9", new[] { "gen9" })]
    public void Test_GetEquivalentFormatIds_ShouldReturnFormatIdThenItsOtherNames(string formatId,
        string[] expectedFormatIds)
    {
        // Act
        var formatIds = FormatAliases.GetEquivalentFormatIds(formatId);

        // Assert
        Assert.That(formatIds, Is.EqualTo(expectedFormatIds));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Test_GetEquivalentFormatIds_ShouldReturnNothing_WhenFormatIdIsEmpty(string formatId)
    {
        // Act
        var formatIds = FormatAliases.GetEquivalentFormatIds(formatId);

        // Assert
        Assert.That(formatIds, Is.Empty);
    }
}
