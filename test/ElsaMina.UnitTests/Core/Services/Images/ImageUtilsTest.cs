using ElsaMina.Core.Services.Images;

namespace ElsaMina.UnitTests.Core.Services.Images;

public class ImageUtilsTest
{
    [Test]
    public void Test_ResizeWithSameAspectRatio_ShouldReturnOriginalDimensions_WhenImageFitsWithinMaxBounds()
    {
        // Arrange
        var width = 400;
        var height = 300;
        var maxWidth = 500;
        var maxHeight = 500;

        // Act
        var (newWidth, newHeight) = ImageUtils.ResizeWithSameAspectRatio(width, height, maxWidth, maxHeight);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(newWidth, Is.EqualTo(400));
            Assert.That(newHeight, Is.EqualTo(300));
        }
    }

    [Test]
    public void Test_ResizeWithSameAspectRatio_ShouldScaleWidth_WhenWidthExceedsMaxBounds()
    {
        // Arrange
        var width = 1000;
        var height = 500;
        var maxWidth = 800;
        var maxHeight = 800;

        // Act
        var (newWidth, newHeight) = ImageUtils.ResizeWithSameAspectRatio(width, height, maxWidth, maxHeight);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(newWidth, Is.EqualTo(800));
            Assert.That(newHeight, Is.EqualTo(400));
        }
    }

    [Test]
    public void Test_ResizeWithSameAspectRatio_ShouldScaleHeight_WhenHeightExceedsMaxBounds()
    {
        // Arrange
        var width = 500;
        var height = 1000;
        var maxWidth = 800;
        var maxHeight = 800;

        // Act
        var (newWidth, newHeight) = ImageUtils.ResizeWithSameAspectRatio(width, height, maxWidth, maxHeight);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(newWidth, Is.EqualTo(400));
            Assert.That(newHeight, Is.EqualTo(800));
        }
    }

    [Test]
    public void Test_ResizeWithSameAspectRatio_ShouldScaleProportionally_WhenBothDimensionsExceedMaxBounds()
    {
        // Arrange
        var width = 1600;
        var height = 1200;
        var maxWidth = 800;
        var maxHeight = 600;

        // Act
        var (newWidth, newHeight) = ImageUtils.ResizeWithSameAspectRatio(width, height, maxWidth, maxHeight);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(newWidth, Is.EqualTo(800));
            Assert.That(newHeight, Is.EqualTo(600));
        }
    }
}