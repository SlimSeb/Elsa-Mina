using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;
using Lusamine.ImageIdentifier;

namespace ElsaMina.Core.Services.Images;

public class ImageService : IImageService
{
    private static readonly ImageIdentifier IMAGE_IDENTIFIER = new();
    private static readonly (int, int) FALLBACK_VALUE = (-1, -1);

    private readonly IHttpService _httpService;

    public ImageService(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public async Task<(int Width, int Height)> GetRemoteImageDimensions(string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stream = await _httpService.SendForStreamAsync(HttpRequest.Get(url), cancellationToken);
            var imageInfo = IMAGE_IDENTIFIER.Identify(stream);
            return imageInfo != null
                ? (imageInfo.Width, imageInfo.Height)
                : FALLBACK_VALUE;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load image");
            return FALLBACK_VALUE;
        }
    }
}