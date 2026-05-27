using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Core.Services.Images;

public class ImageServiceTest
{
    private IHttpService _httpService;
    private ImageService _imageService;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _imageService = new ImageService(_httpService);
    }

    [Test]
    public async Task Test_GetRemoteImageDimensions_ShouldReturnCorrectDimensions_WhenImageLoadsSuccessfully()
    {
        // Arrange
        var assembly = typeof(ImageServiceTest).Assembly;
        var stream = assembly.GetManifestResourceStream(
            "ElsaMina.UnitTests.Core.Services.Images.die.png")!;

        _httpService.SendForStreamAsync(Arg.Any<HttpRequest>()).Returns(stream);

        // Act
        var (width, height) = await _imageService.GetRemoteImageDimensions("http://example.com/image.png");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(width, Is.EqualTo(800));
            Assert.That(height, Is.EqualTo(600));
        }
    }

    [Test]
    public async Task Test_GetRemoteImageDimensions_ShouldReturnMinusOneDimensions_WhenImageFailsToLoad()
    {
        // Arrange
        _httpService.SendForStreamAsync(Arg.Any<HttpRequest>()).Throws(new Exception("Image load failed"));

        // Act
        var (width, height) = await _imageService.GetRemoteImageDimensions("http://example.com/image.png");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(width, Is.EqualTo(-1));
            Assert.That(height, Is.EqualTo(-1));
        }
    }
}