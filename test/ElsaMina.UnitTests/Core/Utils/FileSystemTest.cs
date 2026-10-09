using ElsaMina.Core.Utils;

namespace ElsaMina.UnitTests.Core.Utils;

public class FileSystemTest
{
    [Test]
    public void Test_MakeRelativePath_ShouldReturnRelativePath()
    {
        // Arrange
        var root = Path.GetPathRoot(Path.GetTempPath());
        var absolutePath = Path.Combine(root, "Library", "Stuff", "Lol", "Test", "Stuff.png");
        var referencePath = Path.Combine(root, "Library", "Stuff", "Lol");

        // Act
        var result = FileSystem.MakeRelativePath(absolutePath, referencePath: referencePath);

        // Assert
        Assert.That(result, Is.EqualTo("Lol/Test/Stuff.png"));
    }
}
