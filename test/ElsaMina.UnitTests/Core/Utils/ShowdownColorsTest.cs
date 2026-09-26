using System.Drawing;
using ElsaMina.Core.Utils;

namespace ElsaMina.UnitTests.Core.Utils;

public class ShowdownColorsTests
{
    [Test]
    public void Test_ToColor_ShouldGenerateConsistentColor_ForSameString()
    {
        // Arrange
        const string input = "consistent";

        // Act
        var color1 = input.ToColor();
        var color2 = input.ToColor();

        // Assert
        Assert.That(color1, Is.EqualTo(color2)); // Value equality
    }

    [Test]
    public void Test_ToHexString_ShouldReturnCorrectFormat_WhenCalled()
    {
        // Arrange
        var color = Color.FromArgb(255, 128, 64);

        // Act
        var hexString = color.ToHexString();

        // Assert
        Assert.That(hexString, Is.EqualTo("#FF8040"));
    }

    [Test]
    public void Test_ToHslString_ShouldReturnCorrectHslString_WhenCalled()
    {
        // Arrange
        var color = Color.FromArgb(255, 128, 64);

        // Act
        var hslString = color.ToHslString();

        // Assert
        Assert.That(hslString, Does.StartWith("HSL("));
    }

    [Test]
    public void Test_ToRgbString_ShouldReturnCorrectRgbString_WhenCalled()
    {
        // Arrange
        var color = Color.FromArgb(255, 128, 64);

        // Act
        var rgbString = color.ToRgbString();

        // Assert
        Assert.That(rgbString, Is.EqualTo("RGB(255, 128, 64)"));
    }
}
