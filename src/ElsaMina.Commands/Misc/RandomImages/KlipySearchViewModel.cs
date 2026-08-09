using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipySearchViewModel : LocalizableViewModel
{
    public required IReadOnlyList<KlipyGifThumbnail> Gifs { get; init; }
    public required string Trigger { get; init; }
}
