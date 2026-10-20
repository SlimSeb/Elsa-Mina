using ElsaMina.Battles.Tracking;

namespace ElsaMina.UnitTests.Battles.Tracking;

public class ShowdownIdTest
{
    // Expected values are poke-env's to_id_str in Python, which keeps any Unicode letter
    [TestCase("Mr. Mime", "mrmime")]
    [TestCase("Porygon-Z", "porygonz")]
    [TestCase("Type: Null", "typenull")]
    [TestCase("Farfetch’d", "farfetchd")]
    [TestCase("Flabébé", "flabébé")]
    [TestCase("Ogerpon-Wellspring-Tera", "ogerponwellspringtera")]
    public void Test_ToId_ShouldMatchPokeEnv(string name, string expected)
    {
        // Act
        var id = ShowdownId.ToId(name);

        // Assert
        Assert.That(id, Is.EqualTo(expected));
    }

    [TestCase("Earthquake", "earthquake")]
    [TestCase("return102", "return")]
    [TestCase("frustration", "frustration")]
    [TestCase("Hidden Power Fire 60", "hiddenpower")]
    [TestCase("U-turn", "uturn")]
    public void Test_ToMoveId_ShouldCollapseMoveVariants(string name, string expected)
    {
        // Act
        var id = ShowdownId.ToMoveId(name);

        // Assert
        Assert.That(id, Is.EqualTo(expected));
    }
}
