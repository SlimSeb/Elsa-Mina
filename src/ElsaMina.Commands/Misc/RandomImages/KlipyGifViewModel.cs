using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipyGifViewModel : LocalizableViewModel
{
    public required string Url { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}
