using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.RandomImages;

public class TenorSearchViewModel : LocalizableViewModel
{
    public required IReadOnlyList<TenorGifThumbnail> Gifs { get; init; }
    public required string Trigger { get; init; }
}

public class TenorGifThumbnail
{
    public required string Url { get; init; }
    public int OriginalWidth { get; init; }
    public int OriginalHeight { get; init; }
    public int ThumbWidth { get; init; }
    public int ThumbHeight { get; init; }
}
