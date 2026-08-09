namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipyGifThumbnail
{
    /// <summary>
    /// Small variant rendered inside the mosaic.
    /// </summary>
    public required string PreviewUrl { get; init; }

    /// <summary>
    /// Larger variant the "Send" button posts in chat.
    /// </summary>
    public required string FullUrl { get; init; }

    public int FullWidth { get; init; }
    public int FullHeight { get; init; }
    public int ThumbWidth { get; init; }
    public int ThumbHeight { get; init; }
}
