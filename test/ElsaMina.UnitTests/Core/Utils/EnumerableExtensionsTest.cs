using ElsaMina.Core.Utils;

namespace ElsaMina.UnitTests.Core.Utils;

public class EnumerableExtensionsTest
{
    [Test]
    public void Test_Enumerate_ShouldReturnIndexedValues()
    {
        // Arrange
        List<string> list = ["a", "b", "c"];

        // Act
        var result = list.Enumerate().ToList();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result[0], Is.EqualTo((0, "a")));
            Assert.That(result[1], Is.EqualTo((1, "b")));
            Assert.That(result[2], Is.EqualTo((2, "c")));
        }
    }
}