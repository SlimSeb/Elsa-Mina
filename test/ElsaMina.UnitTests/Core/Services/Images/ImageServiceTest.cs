using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
        var image = new Image<Rgba32>(100, 200);
        var memoryStream = new MemoryStream();
        await image.SaveAsPngAsync(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        _httpService.GetStreamAsync(Arg.Any<string>()).Returns(memoryStream);

        // Act
        var (width, height) = await _imageService.GetRemoteImageDimensions("http://example.com/image.png");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(width, Is.EqualTo(100));
            Assert.That(height, Is.EqualTo(200));
        }
    }

    [Test]
    public async Task Test_GetRemoteImageDimensions_ShouldReturnMinusOneDimensions_WhenImageFailsToLoad()
    {
        // Arrange
        _httpService.GetStreamAsync(Arg.Any<string>()).Throws(new Exception("Image load failed"));

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